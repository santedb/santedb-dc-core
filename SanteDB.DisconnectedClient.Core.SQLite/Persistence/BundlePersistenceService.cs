/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2021-2-9
 */
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.DataType;
using SQLite.Net.Interop;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents a bundle persistence service
    /// </summary>
    public class BundlePersistenceService : IdentifiedPersistenceService<Bundle, DbBundle>
    {
        /// <summary>
        /// Cannot query for bundles
        /// </summary>
        protected override IEnumerable<Bundle> QueryInternal(SQLiteDataContext context, Expression<Func<Bundle, bool>> query, int offset, int count, out int totalResults, Guid queryId, bool countResults, ModelSort<Bundle>[] orderBy)
        {
            totalResults = 0;
            return new List<Bundle>();
        }

        /// <summary>
        /// Connot query bundles
        /// </summary>
        protected override IEnumerable<Bundle> QueryInternal(SQLiteDataContext context, string storedQueryName, IDictionary<string, object> parms, int offset, int count, out int totalResults, Guid queryId, bool countResults, ModelSort<Bundle>[] orderBy)
        {
            totalResults = 0;
            return new List<Bundle>();

        }

        /// <summary>
        /// Bundles are special, they may be written on the current connection
        /// or in memory
        /// </summary>
        public override Bundle Insert(Bundle data, TransactionMode mode, IPrincipal principal)
        {
            // first, are we just doing a normal insert?
            if (data.Item.Count <= 250)
                return base.Insert(data, mode, principal);
            else
            { // It is cheaper to open a mem-db and let other threads access the main db for the time being

                base.FireInserting(new DataPersistingEventArgs<Bundle>(data, mode, principal));
                var cstr = ApplicationContext.Current.ConfigurationManager.GetConnectionString("santeDbData");
                // Memory connection
                using (var memConnection = new LockableSQLiteConnection(ApplicationContext.Current.GetService<ISQLitePlatform>(), new SanteDB.Core.Configuration.Data.ConnectionString()
                {
                    Value = "dbfile=:memory:"
                }, SQLiteOpenFlags.ReadWrite))
                {
                    try
                    {
                        ApplicationContext.Current.SetProgress(Strings.locale_prepareBundle, 0.5f);
                        // We want to apply the initial schema
                        new SanteDB.DisconnectedClient.SQLite.Configuration.Data.Migrations.InitialCatalog().Install(memConnection, true);


                        // Copy the name component and address component values
                        if (cstr.GetComponent("encrypt") != "true" || ApplicationContext.Current.GetCurrentContextSecurityKey() == null)
                            memConnection.Execute($"ATTACH DATABASE '{cstr.GetComponent("dbfile")}' AS file_db KEY ''");
                        else
                            memConnection.Execute($"ATTACH DATABASE '{cstr.GetComponent("dbfile")}' AS file_db KEY X'{BitConverter.ToString(ApplicationContext.Current.GetCurrentContextSecurityKey()).Replace("-", "")}'");

                        try
                        {
                            memConnection.BeginTransaction();

                            //// Names & Address
                            memConnection.Execute($"INSERT OR REPLACE INTO phonetic_value (uuid, value) SELECT uuid, value FROM file_db.phonetic_value");
                            memConnection.Execute($"INSERT OR REPLACE INTO entity_addr_val (uuid, value) SELECT uuid, value FROM file_db.entity_addr_val");

                            //foreach (var itm in memConnection.Query<String>("SELECT NAME FROM SQLITE_MASTER WHERE TYPE = 'index' AND SQL IS NOT NULL"))
                            //    memConnection.Execute(String.Format("DROP INDEX {0};", itm));
                            memConnection.Commit();
                        }
                        catch (Exception e)
                        {
                            memConnection.Rollback();
                            throw new DataException("Error inserting bundle", e);
                        }

                        memConnection.Execute("DETACH DATABASE file_db");

                        // We insert in the memcontext now
                        using (var memContext = new SQLiteDataContext(memConnection, principal))
                            this.InsertInternal(memContext, data);

                        var columnMapping = memConnection.TableMappings.Where(o => o.MappedType.Namespace.StartsWith("SanteDB")).ToList();

                        // Now we attach our local file based DB by requesting a lock so nobody else touches it!
                        using (var fileContext = this.CreateConnection(principal))
                        using (fileContext.LockConnection())
                        {
                            if (cstr.GetComponent("encrypt") != "true" || ApplicationContext.Current.GetCurrentContextSecurityKey() == null)
                                memConnection.Execute($"ATTACH DATABASE '{cstr.GetComponent("dbfile")}' AS file_db KEY ''");
                            else
                                memConnection.Execute($"ATTACH DATABASE '{cstr.GetComponent("dbfile")}' AS file_db KEY X'{BitConverter.ToString(ApplicationContext.Current.GetCurrentContextSecurityKey()).Replace("-", "")}'");

                            try
                            {
                                memConnection.BeginTransaction();

                                // Copy copy!!!
                                int i = 0;
                                foreach (var tbl in memConnection.Query<SysInfoStruct>("SELECT name FROM sqlite_master WHERE type='table'"))
                                {
                                    memConnection.Execute($"INSERT OR REPLACE INTO file_db.{tbl.Name} SELECT * FROM {tbl.Name}");
                                    ApplicationContext.Current.SetProgress(Strings.locale_committing, (float)i++ / columnMapping.Count);

                                }
                                ApplicationContext.Current.SetProgress(Strings.locale_committing, 1.0f);

                                memConnection.Commit();
                            }
                            catch
                            {
                                memConnection.Rollback();
                                throw;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceWarning("Error inserting bundle using fast insert method - Will use slow method: {0}", e);
                        return base.Insert(data, mode, principal); // Attempt to do a slow insert 
                        // TODO: Figure out why the copy command sometimes is missing UUIDs
                        //throw new LocalPersistenceException(Synchronization.Model.DataOperationType.Insert, data, e);
                    }
                }

                base.FireInserted(new DataPersistedEventArgs<Bundle>(data, mode, principal));
                return data;
            }
        }

        /// <summary>
        /// Insert the bundle
        /// </summary>
        protected override Bundle InsertInternal(SQLiteDataContext context, Bundle data)
        {
            // Prepare to insert a bundle
            for (int i = 0; i < data.Item.Count; i++)
            {
                var itm = data.Item[i];
                if (itm is null)
                    continue;
                try
                {

#if SHOW_STATUS || PERFMON
                Stopwatch itmSw = new Stopwatch();
                itmSw.Start();
#endif
                    var idp = typeof(IDataPersistenceService<>).MakeGenericType(new Type[] { itm.GetType() });
                    var svc = ApplicationContext.Current.GetService(idp) as ISQLitePersistenceService;
                    if (svc == null) continue; // can't insert
                    if (context.Connection.DatabasePath != ":memory:" && itm.TryGetExisting(context, true) != null)
                        data.Item[i] = svc.Update(context, itm) as IdentifiedData;
                    else
                        data.Item[i] = svc.Insert(context, itm) as IdentifiedData;
#if SHOW_STATUS || PERFMON
                itmSw.Stop();
#endif
                    if (i % 100 == 0 && data.Item.Count > 500)
                        ApplicationContext.Current.SetProgress(String.Format(Strings.locale_processBundle, itm.GetType().Name, i, data.Item.Count), i / (float)data.Item.Count);
#if PERFMON
                ApplicationContext.Current.PerformanceLog(nameof(BundlePersistenceService), nameof(InsertInternal), $"Insert{itm.GetType().Name}", itmSw.Elapsed);
#endif
                }
                catch (Exception e)
                {
                    throw new DataException($"Error inserting bundle item {itm} (item #{i})", e);
                }
            }


            return data;
        }

        /// <summary>
        /// Update everything in the bundle
        /// </summary>
        protected override Bundle UpdateInternal(SQLiteDataContext context, Bundle data)
        {
            foreach (var itm in data.Item)
            {
                var idp = typeof(IDataPersistenceService<>).MakeGenericType(new Type[] { itm.GetType() });
                var instance = ApplicationContext.Current.GetService(idp) as ISQLitePersistenceService;
                if (instance != null)
                    itm.CopyObjectData(instance.Update(context, itm));
            }
            return data;
        }

        /// <summary>
        /// Obsolete everything in the bundle
        /// </summary>
        protected override Bundle ObsoleteInternal(SQLiteDataContext context, Bundle data)
        {
            foreach (var itm in data.Item)
            {
                var idp = typeof(IDataPersistenceService<>).MakeGenericType(new Type[] { itm.GetType() });
                var instance = ApplicationContext.Current.GetService(idp) as ISQLitePersistenceService;
                if (instance != null)
                    itm.CopyObjectData(instance.Obsolete(context, itm));
            }
            return data;
        }

    }
}
