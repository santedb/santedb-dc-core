/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Search.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SanteDB.Matcher.Configuration;
using System.IO;
using System.Reflection;

namespace SanteDB.DisconnectedClient.SQLite.Search
{
    /// <summary>
    /// Search indexing service
    /// </summary>
    public class SQLiteSearchIndexService : IFreetextSearchService, IDaemonService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "SQLite FreeText Indexing Service";

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteSearchIndexService));

        // Is bound?
        private bool m_patientBound = false;
        private bool m_bundleBound = false;

        /// <summary>
        /// Returns whether the service is running or not
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return true;
            }
        }

        // Lock object
        private object m_lock = new object();
        /// <summary>
        /// The service is starting
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// The service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// The service has stopped
        /// </summary>
        public event EventHandler Stopped;
        /// <summary>
        /// The service is stopping
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        /// Create a connection
        /// </summary>
        private LockableSQLiteConnection CreateConnection()
        {
            return SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString("santeDbSearch"));
        }

        /// <summary>
        /// Search based on already tokenized string
        /// </summary>
        public IEnumerable<TEntity> Search<TEntity>(String[] tokens, int offset, int? count, out int totalResults, ModelSort<TEntity>[] orderBy) where TEntity : IdentifiedData
        {
            try
            {
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    // Search query builder
                    StringBuilder queryBuilder = new StringBuilder();

                    queryBuilder.AppendFormat("SELECT DISTINCT {1}.* FROM {0} INNER JOIN {1} ON ({0}.entity = {1}.key) WHERE {1}.type = '{2}' AND {0}.entity IN (",
                        conn.GetMapping<Model.SearchTermEntity>().TableName,
                        conn.GetMapping<Model.SearchEntityType>().TableName,
                        typeof(TEntity).FullName);

                    var approxConfig = ApplicationContext.Current.Configuration.GetSection<ApproximateMatchingConfigurationSection>();
                    var hasSoundex = conn.ExecuteScalar<Int32>("select sqlite_compileoption_used('SQLITE_SOUNDEX');") == 1;
                    bool hasSpellFix = false;
                    if (conn.ExecuteScalar<Int32>("SELECT sqlite_compileoption_used('SQLITE_ENABLE_LOAD_EXTENSION')") == 1)
                    {
                        conn.Platform.SQLiteApi.EnableLoadExtension(conn.Handle, 1);
                        try
                        {
                            try
                            {
                                conn.ExecuteScalar<Int32>("SELECT editdist3('__sfEditCost');");
                                hasSpellFix = conn.ExecuteScalar<Int32>("SELECT editdist3('test','test1');") > 0;

                            }
                            catch
                            {
                                conn.ExecuteScalar<String>("SELECT load_extension('spellfix');");
                                hasSpellFix = conn.ExecuteScalar<Int32>("SELECT editdist3('test','test1');") > 0;
                            }
                        }
                        catch(Exception e) {
                            this.m_tracer.TraceWarning("Will not be using SpellFix plugin for SQLite - {0}", e.Message);
                        }
                    }

                    foreach (var tkn in tokens.SelectMany(t=>t.Split(' ')))
                    {
                        queryBuilder.AppendFormat("SELECT {0}.entity FROM {0} INNER JOIN {1} ON ({0}.term = {1}.key) WHERE ",
                                conn.GetMapping<Model.SearchTermEntity>().TableName,
                                conn.GetMapping<Model.SearchTerm>().TableName,
                                typeof(TEntity).FullName);
                        
                        if (approxConfig != null)
                        {
                            queryBuilder.Append("(");
                            foreach(var alg in approxConfig.ApproxSearchOptions.Where(o=>o.Enabled))
                            {
                                if (alg is ApproxPatternOption pattern)
                                    queryBuilder.AppendFormat("{0}.term LIKE '{1}' OR ", conn.GetMapping<Model.SearchTerm>().TableName, tkn.Replace("'", "''").Replace("*", "%"));
                                else if (alg is ApproxPhoneticOption phonetic && hasSoundex)
                                    queryBuilder.AppendFormat("SOUNDEX({0}.term) = SOUNDEX('{1}') OR ", conn.GetMapping<Model.SearchTerm>().TableName, tkn.Replace("'", "''"));
                                else if (alg is ApproxDifferenceOption difference && hasSpellFix)
                                    queryBuilder.AppendFormat("(length({0}.term) > {2} * 2 and editdist3(trim(lower({0}.term)), trim(lower('{1}'))) <= {2}) OR ", conn.GetMapping<Model.SearchTerm>().TableName, tkn.Replace("'", "''"), difference.MaxDifference);

                            }
                            queryBuilder.Remove(queryBuilder.Length - 3, 3);
                            queryBuilder.Append(")");
                        }
                        else if (tkn.Contains("*"))
                            queryBuilder.AppendFormat("{0}.term LIKE '{1}' ", conn.GetMapping<Model.SearchTerm>().TableName, tkn.Replace("'", "''").Replace("*", "%"));
                        else
                            queryBuilder.AppendFormat("{0}.term = '{1}' ", conn.GetMapping<Model.SearchTerm>().TableName, tkn.ToLower().Replace("'", "''"));
                        queryBuilder.Append(" INTERSECT ");
                    }

                    queryBuilder.Remove(queryBuilder.Length - 11, 11);
                    queryBuilder.Append(")");

                    
                    // Search now!
                    this.m_tracer.TraceVerbose("FREETEXT SEARCH: {0}", queryBuilder);

                    // Perform query
                    var results = conn.Query<Model.SearchEntityType>(queryBuilder.ToString());

                    var persistence = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();
                    totalResults = results.Count();

                    var retVal = results.Skip(offset).Take(count ?? 100).AsParallel().AsOrdered().Select(o => persistence.Get(new Guid(o.Key), null, false, AuthenticationContext.Current.Principal));

                    // Sorting (well as best we can for FTS)
                    if(orderBy.Length > 0)
                    {
                        var order = orderBy.First();
                        if(order.SortOrder == SanteDB.Core.Model.Map.SortOrderType.OrderBy)
                            retVal = retVal.OrderBy(order.SortProperty.Compile());
                        else
                            retVal = retVal.OrderByDescending(order.SortProperty.Compile());
                    }

                    return retVal;
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error performing search: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Perform an index of the entity
        /// </summary>
        internal bool IndexEntity(params Entity[] entities)
        {
            if (entities.Length == 0) return true;

            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    conn.BeginTransaction();

                    foreach (var e in entities)
                    {
                        var entityUuid = e.Key.Value.ToByteArray();
                        var entityVersionUuid = e.VersionKey.Value.ToByteArray();

                        if (conn.Table<SearchEntityType>().Where(o => o.Key == entityUuid && o.VersionKey == entityVersionUuid).Count() > 0) continue; // no change
                        else if (e.LoadCollection<EntityTag>("Tags").Any(t => t.TagKey == "isAnonymous")) // Anonymous?
                            continue;

                        var tokens = (e.Names ?? new List<EntityName>()).SelectMany(o => o.Component.SelectMany(c => c.Value.Split(' ')).Select(c=>c.Trim().ToLower()))
                        .Union((e.Identifiers ?? new List<EntityIdentifier>()).Select(o => o.Value.ToLower()))
                        .Union((e.Addresses ?? new List<EntityAddress>()).SelectMany(o => o.Component.Select(c => c.Value.Trim().ToLower()).Where(c=>c.Length > 2)))
                        .Union((e.Telecoms ?? new List<EntityTelecomAddress>()).Select(o => o.Value.ToLower()))
                        .Union((e.Relationships ?? new List<EntityRelationship>()).Where(o => o.TargetEntity is Person).SelectMany(o => (o.TargetEntity.Names ?? new List<EntityName>()).SelectMany(n => n.Component?.Select(c => c.Value?.Trim().ToLower()))))
                        .Union((e.Relationships ?? new List<EntityRelationship>()).Where(o => o.TargetEntity is Person).SelectMany(o => (o.TargetEntity.Telecoms ?? new List<EntityTelecomAddress>()).Select(c => c.Value?.Trim().ToLower())))
                        .Where(o => o != null);

                        // Insert new terms
                        var existing = conn.Table<SearchTerm>().Where(o => tokens.Contains(o.Term)).ToArray();
                        var inserting = tokens.Where(t => !existing.Any(x => x.Term == t)).Select(o => new SearchTerm() { Term = o }).ToArray();
                        conn.InsertAll(inserting);

                        this.m_tracer.TraceVerbose("{0}", e);
                        foreach (var itm in existing.Union(inserting))
                            this.m_tracer.TraceVerbose("\t+{0}", itm.Term);

                        // Now match tokens with this 
                        conn.Execute(String.Format(String.Format("DELETE FROM {0} WHERE entity = ?", conn.GetMapping<SearchTermEntity>().TableName), e.Key.Value.ToByteArray()));
                        conn.Delete<SearchEntityType>(e.Key.Value.ToByteArray());

                        if (!e.ObsoletionTime.HasValue)
                        {
                            var insertRefs = existing.Union(inserting).Distinct().Select(o => new SearchTermEntity() { EntityId = e.Key.Value.ToByteArray(), TermId = o.Key }).ToArray();
                            conn.InsertAll(insertRefs);
                            conn.Insert(new SearchEntityType() { Key = e.Key.Value.ToByteArray(), SearchType = e.GetType().FullName, VersionKey = e.VersionKey.Value.ToByteArray() });
                        }

                        this.m_tracer.TraceInfo("Indexed {0}", e);
                        
                    }

                    // Now commit
                    conn.Commit();

                    return true;
                }
                catch (Exception ex)
                {
                    this.m_tracer.TraceError("Error indexing {0} : {1}", entities, ex);
                    conn.Rollback();
                    return false;
                }
            }
        }

        /// <summary>
        /// Perform an index of the entity
        /// </summary>
        internal bool DeleteEntity(Entity e)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    conn.BeginTransaction();

                    // Now match tokens with this 
                    conn.Execute(String.Format(String.Format("DELETE FROM {0} WHERE entity = ?", conn.GetMapping<SearchTermEntity>().TableName), e.Key.Value.ToByteArray()));
                    conn.Delete<SearchEntityType>(e.Key.Value.ToByteArray());

                    // Now commit
                    conn.Commit();

                    return true;
                }
                catch (Exception ex)
                {
                    this.m_tracer.TraceError("Error indexing {0} : {1}", e, ex);
                    conn.Rollback();
                    return false;
                }
            }
        }

        /// <summary>
        /// Queue indexing in the background
        /// </summary>
        private bool IndexBackground(Entity e)
        {
            ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem((o) => this.IndexEntity(o as Entity), e);
            return true;
        }

        /// <summary>
        /// Start the service. 
        /// </summary>
        /// <remarks>In reality this forces a background re-index of the database and subscription to the entity persistence services
        /// to update the index where possible</remarks>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            // After start then we want to start up the indexer
            ApplicationContext.Current.Started += (so, se) =>
            {
                try
                {
                    // Subscribe to persistence events which will have an impact on the index
                    var patientPersistence = ApplicationContext.Current.GetService<IDataPersistenceService<Patient>>();
                    var bundlePersistence = ApplicationContext.Current.GetService<IDataPersistenceService<Bundle>>();

                    // Bind entity
                    if (patientPersistence != null && !this.m_patientBound)
                    {
                        patientPersistence.Inserted += (o, e) => this.IndexBackground(e.Data);
                        patientPersistence.Updated += (o, e) => this.IndexBackground(e.Data);
                        patientPersistence.Obsoleted += (o, e) => this.DeleteEntity(e.Data);
                        this.m_patientBound = true;
                    }

                    // Bind entity
                    if (bundlePersistence != null && !this.m_bundleBound)
                    {
                        Action<Object> doBundleIndex = (o) =>
                        {
                            this.IndexEntity((o as Bundle).Item.Where(e => e is Patient).OfType<Entity>().ToArray());
                        };

                        bundlePersistence.Inserted += (o, e) => ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(doBundleIndex, e.Data);
                        bundlePersistence.Updated += (o, e) => ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(doBundleIndex, e.Data);
                        bundlePersistence.Obsoleted += (o, e) => ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem((data) => {
                            foreach (var itm in (data as Bundle).Item.OfType<Patient>())
                                this.DeleteEntity(itm);
                        }, e.Data);
                    }

                    ApplicationContext.Current.GetService<IJobManagerService>().AddJob(new SQLiteSearchIndexRefreshJob(), new TimeSpan(0, 10, 0));
                    this.Started?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error starting search index: {0}", e);
                }

            };

            return true;
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        /// <returns></returns>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

      
    }
}
