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
using SanteDB.DisconnectedClient.SQLite.Query;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Hacks;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;
using SQLite.Net;
using SanteDB.Core.Exceptions;

namespace SanteDB.DisconnectedClient.SQLite
{
    /// <summary>
    /// Represents a data persistence service which stores data in the local SQLite data store
    /// </summary>
    public abstract class SQLitePersistenceServiceBase<TData> : 
        IDataPersistenceService<TData>, 
        IStoredQueryDataPersistenceService<TData>,
        ISQLitePersistenceService,
        IDataPersistenceService
    where TData : IdentifiedData, new()
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => $"SQLite Data Persistence Service for {typeof(TData).FullName}";

        // Get tracer
        protected Tracer m_tracer; //= Tracer.GetTracer(typeof(LocalPersistenceServiceBase<TData>));

        // Configuration
        protected static DcDataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>();

        // Mapper
        protected static ModelMapper m_mapper;

        // Builder
        protected static QueryBuilder m_builder;

        // Static CTOR
        static SQLitePersistenceServiceBase()
        {

            m_mapper = SQLitePersistenceService.Mapper;
            m_builder = new QueryBuilder(m_mapper, new RelationshipQueryHack(),
                new ConceptQueryHack(m_mapper));
        }

        public SQLitePersistenceServiceBase()
        {
            this.m_tracer = Tracer.GetTracer(this.GetType());
        }

        #region IDataPersistenceService implementation
        /// <summary>
        /// Occurs when inserted.
        /// </summary>
        public event EventHandler<DataPersistedEventArgs<TData>> Inserted;
        /// <summary>
        /// Occurs when inserting.
        /// </summary>
        public event EventHandler<DataPersistingEventArgs<TData>> Inserting;
        /// <summary>
        /// Occurs when updated.
        /// </summary>
        public event EventHandler<DataPersistedEventArgs<TData>> Updated;
        /// <summary>
        /// Occurs when updating.
        /// </summary>
        public event EventHandler<DataPersistingEventArgs<TData>> Updating;
        /// <summary>
        /// Occurs when obsoleted.
        /// </summary>
        public event EventHandler<DataPersistedEventArgs<TData>> Obsoleted;
        /// <summary>
        /// Occurs when obsoleting.
        /// </summary>
        public event EventHandler<DataPersistingEventArgs<TData>> Obsoleting;
        /// <summary>
        /// Occurs when queried.
        /// </summary>
        public event EventHandler<QueryResultEventArgs<TData>> Queried;
        /// <summary>
        /// Occurs when querying.
        /// </summary>
        public event EventHandler<QueryRequestEventArgs<TData>> Querying;
        /// <summary>
        /// Data has been retrieved
        /// </summary>
        public event EventHandler<DataRetrievingEventArgs<TData>> Retrieving;
        /// <summary>
        /// Occurs when querying.
        /// </summary>
        public event EventHandler<DataRetrievedEventArgs<TData>> Retrieved;

        /// <summary>
        /// Fire inserting event
        /// </summary>
        protected void FireInserting(DataPersistingEventArgs<TData> evt)
        {
            this.Inserting?.Invoke(this, evt);
        }

        /// <summary>
        /// Fire inserting event
        /// </summary>
        protected void FireInserted(DataPersistedEventArgs<TData> evt)
        {
            this.Inserted?.Invoke(this, evt);
        }

        /// <summary>
        /// Creates the connection.
        /// </summary>
        /// <returns>The connection.</returns>
        protected SQLiteDataContext CreateConnection(IPrincipal principal)
        {
            return new SQLiteDataContext(SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(m_configuration.MainDataSourceConnectionStringName)), principal);
        }

        /// <summary>
        /// Create readonly connection
        /// </summary>
        protected SQLiteDataContext CreateReadonlyConnection(IPrincipal principal)
        {
            return new SQLiteDataContext(SQLiteConnectionManager.Current.GetReadonlyConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(m_configuration.MainDataSourceConnectionStringName)), principal);
        }

        /// <summary>
        /// Insert the specified data.
        /// </summary>
        /// <param name="data">Data.</param>
        public virtual TData Insert(TData data, TransactionMode mode, IPrincipal principal)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            DataPersistingEventArgs<TData> preArgs = new DataPersistingEventArgs<TData>(data, principal);
            this.Inserting?.Invoke(this, preArgs);
            if (preArgs.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event handler indicates abort insert for {0}", data);
                return data;
            }

#if PERFMON
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif

            // Persist objectel
            using (var context = this.CreateConnection(principal))
                try
                {
                    using (context.LockConnection())
                    {
                        try
                        {

                            // Does this object already exist?
                            if (data.Key.HasValue && this.Get(context, data.Key.Value) != null)
                                throw new DuplicateKeyException($"Object {typeof(TData).Name} with key {data.Key} already exists");

                            this.m_tracer.TraceVerbose("INSERT {0}", data);

                            context.Connection.BeginTransaction();
                            data = this.Insert(context, data);

                            if (mode == TransactionMode.Commit)
                                context.Connection.Commit();
                            else
                                context.Connection.Rollback();
                            // Remove from the cache
                            foreach (var itm in context.CacheOnCommit.AsParallel())
                                ApplicationContext.Current.GetService<IDataCachingService>().Add(itm);
                        }
                        catch (Exception e)
                        {
                            this.m_tracer.TraceError("Error : {0}", e);
                            context.Connection.Rollback();
                            throw new LocalPersistenceException(SynchronizationOperationType.Insert, data, e);
                        }

                    }
                    this.Inserted?.Invoke(this, new DataPersistedEventArgs<TData>(data, principal));
                    return data;
                }
                catch (SQLiteException e)
                {
                    this.m_tracer.TraceError("Error inserting data {1} : {0}", e, context.Connection);
                    throw new DataPersistenceException($"Database error inserting {data}", e);
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException($"Error inserting {data}", e);
                }

#if PERFMON
            finally
            {
                sw.Stop();
                    ApplicationContext.Current.PerformanceLog(typeof(TData).Name, nameof(Insert), "Complete", sw.Elapsed);
            }
#endif

        }

        /// <summary>
        /// Update the specified data
        /// </summary>
        /// <param name="data">Data.</param>
        public virtual TData Update(TData data, TransactionMode mode, IPrincipal principal)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            else if (!data.Key.HasValue || data.Key == Guid.Empty)
                throw new InvalidOperationException("Data missing key");

            DataPersistingEventArgs<TData> preArgs = new DataPersistingEventArgs<TData>(data, principal);
            this.Updating?.Invoke(this, preArgs);
            if (preArgs.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event handler indicates abort update for {0}", data);
                return data;
            }
#if PERFMON
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif
            // Persist object
            using (var context = this.CreateConnection(principal))
                try
                {
                    using (context.LockConnection())
                    {
                        try
                        {
                            this.m_tracer.TraceVerbose("UPDATE {0}", data);
                            context.Connection.BeginTransaction();

                            data = this.Update(context, data);

                            if (mode == TransactionMode.Commit)
                                context.Connection.Commit();
                            else
                                context.Connection.Rollback();

                            // Remove from the cache
                            foreach (var itm in context.CacheOnCommit.AsParallel())
                                ApplicationContext.Current.GetService<IDataCachingService>().Add(itm);

                        }
                        catch (Exception e)
                        {
                            this.m_tracer.TraceError("Error : {0}", e);
                            context.Connection.Rollback();
                            throw new LocalPersistenceException(SynchronizationOperationType.Update, data, e);

                        }
                    }
                    this.Updated?.Invoke(this, new DataPersistedEventArgs<TData>(data,principal));
                    return data;
                }
                catch (SQLiteException e)
                {
                    this.m_tracer.TraceError("Error updating data {1} : {0}", e, context.Connection);
                    throw new DataPersistenceException($"Database error obsoleting {data}", e);
                }
                catch (Exception e) {
                    throw new DataPersistenceException($"Error updating {data}", e);
                }
#if PERFMON
                finally
                {
                    sw.Stop();
                    ApplicationContext.Current.PerformanceLog(typeof(TData).Name, nameof(Update), "Complete", sw.Elapsed);
                }
#endif
        }

        /// <summary>
        /// Obsolete the specified identified data
        /// </summary>
        /// <param name="data">Data.</param>
        public virtual TData Obsolete(TData data, TransactionMode mode, IPrincipal principal)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            else if (!data.Key.HasValue || data.Key == Guid.Empty)
                throw new InvalidOperationException("Data missing key");
#if PERFMON
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif
            DataPersistingEventArgs<TData> preArgs = new DataPersistingEventArgs<TData>(data, principal);
            this.Obsoleting?.Invoke(this, preArgs);
            if (preArgs.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event handler indicates abort for {0}", data);
                return data;
            }

            // Obsolete object
            using (var context = this.CreateConnection(principal))
                try
                {
                    using (context.LockConnection())
                    {
                        try
                        {
                            this.m_tracer.TraceVerbose("OBSOLETE {0}", data);
                            context.Connection.BeginTransaction();

                            data = this.Obsolete(context, data);

                            if (mode == TransactionMode.Commit)
                                context.Connection.Commit();
                            else
                                context.Connection.Rollback();

                            // Remove from the cache
                            foreach (var itm in context.CacheOnCommit.AsParallel())
                                ApplicationContext.Current.GetService<IDataCachingService>().Remove(itm.Key.Value);

                        }
                        catch (Exception e)
                        {
                            this.m_tracer.TraceError("Error : {0}", e);
                            context.Connection.Rollback();
                            throw new LocalPersistenceException(SynchronizationOperationType.Obsolete, data, e);
                        }
                    }
                    this.Obsoleted?.Invoke(this, new DataPersistedEventArgs<TData>(data, principal));

                    return data;
                }
                catch (SQLiteException e)
                {
                    this.m_tracer.TraceError("Error obsoleting data {1} : {0}", e, context.Connection);
                    throw new DataPersistenceException($"Database error obsoleting {data}", e);
                }
                catch (Exception e) { throw new DataPersistenceException($"Error obsoleting {data}", e); }
#if PERFMON
                finally
                {
                    sw.Stop();
                    ApplicationContext.Current.PerformanceLog(typeof(TData).Name, nameof(Obsolete), "Complete", sw.Elapsed);
                }

#endif
        }

        /// <summary>
        /// Get the specified key.
        /// </summary>
        /// <param name="key">Key.</param>
        public virtual TData Get(Guid key, Guid? versionKey, bool loadFast, IPrincipal principal)
        {
            if (key == Guid.Empty) return null;
            var existing = ApplicationContext.Current.GetService<IDataCachingService>().GetCacheItem(key);
            if ((existing as IdentifiedData)?.LoadState <= LoadState.FullLoad)
            {
                using (var context = this.CreateReadonlyConnection(principal))
                    try
                    {
                        using (context.LockConnection())
                        {
                            (existing as IdentifiedData).LoadAssociations(context);
                        }
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Error loading associations: {0}", e);
                    }
            }
            if (existing != null)
                return existing as TData;
            int toss = 0;
            this.m_tracer.TraceInfo("GET: {0}", key);
            return this.Query(o => o.Key == key, null, 0, 1, out toss, false, false, principal, null)?.SingleOrDefault();
        }

        /// <summary>
        /// Query the specified data
        /// </summary>
        /// <param name="query">Query.</param>
        public virtual System.Collections.Generic.IEnumerable<TData> Query(System.Linq.Expressions.Expression<Func<TData, bool>> query, IPrincipal principal)
        {
            int totalResults = 0;
            return this.Query(query, null, 0, null, out totalResults, false, false, principal, null);
        }

        /// <summary>
        /// Query the specified data
        /// </summary>
        /// <param name="query">Query.</param>
        public virtual System.Collections.Generic.IEnumerable<TData> Query(System.Linq.Expressions.Expression<Func<TData, bool>> query, int offset, int? count, out int totalResults, IPrincipal principal, params ModelSort<TData>[] orderBy)
        {
            return this.Query(query, null, offset, count, out totalResults, true, false, principal, orderBy);
        }

        /// <summary>
        /// Query the specified data
        /// </summary>
        /// <param name="query">Query.</param>
        public virtual System.Collections.Generic.IEnumerable<TData> QueryFast(System.Linq.Expressions.Expression<Func<TData, bool>> query, Guid queryId, int offset, int? count, out int totalResults, IPrincipal principal)
        {
            return this.Query(query, queryId, offset, count, out totalResults, true, true, principal, null);
        }
        
        /// <summary>
        /// Query function returning results and count control
        /// </summary>
        private IEnumerable<TData> Query(System.Linq.Expressions.Expression<Func<TData, bool>> query, Guid? queryId, int offset, int? count, out int totalResults, bool countResults, bool fastQuery, IPrincipal principal, ModelSort<TData>[] orderBy)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            QueryRequestEventArgs<TData> preArgs = new QueryRequestEventArgs<TData>(query, offset, count, queryId, principal);
            this.Querying?.Invoke(this, preArgs);
            if (preArgs.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event handler indicates abort query {0}", query);
                totalResults = preArgs.TotalResults;
                return preArgs.Results;
            }

#if PERFMON
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif
            // Query object
            using (var context = this.CreateReadonlyConnection(principal))
                try
                {
                    IEnumerable<TData> results = null;
                    using (context.LockConnection())
                    {
                        this.m_tracer.TraceVerbose("QUERY {0}", query);

                        if (fastQuery)
                            context.DelayLoadMode = LoadState.PartialLoad;
                        else
                            context.DelayLoadMode = LoadState.FullLoad;
                        
                        results = this.Query(context, query, queryId.GetValueOrDefault(), offset, count ?? -1, out totalResults, countResults, orderBy);
                    }

                    var postData = new QueryResultEventArgs<TData>(query, results, offset, count, totalResults, queryId, principal);
                    this.Queried?.Invoke(this, postData);

                    totalResults = postData.TotalResults;

                    // Remove from the cache
                    foreach (var itm in context.CacheOnCommit.AsParallel())
                        ApplicationContext.Current.GetService<IDataCachingService>()?.Add(itm);

                    return postData.Results;


                }
                catch(SQLiteException e)
                {
                    this.m_tracer.TraceError("Error executing query {1} : {0}", e, context.Connection);
                    throw new DataPersistenceException($"Data error executing query againt {typeof(TData)}", e);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error : {0}", e);
                    throw new DataPersistenceException($"Error executing query against {typeof(TData)}", e);
                }
#if PERFMON
                finally
                {
                    sw.Stop();
                    ApplicationContext.Current.PerformanceLog(typeof(TData).Name, nameof(Query), query.ToString(), sw.Elapsed);
                }

#endif

        }

        /// <summary>
        /// Perform a count
        /// </summary>
        public virtual long Count(Expression<Func<TData, bool>> query, IPrincipal principal)
        {
            var tr = 0;
            this.Query(query, null, 0, 0, out tr, true, true, principal, null);
            return tr;
        }

      
        #endregion

        /// <summary>
        /// Get the current user UUID.
        /// </summary>
        /// <returns>The user UUID.</returns>
        protected Guid CurrentUserUuid(SQLiteDataContext context)
        {
            // Is there a preferred SID on the context claim?
            if (context.Principal is IClaimsPrincipal)
            {
                var cprincipal = context.Principal as IClaimsPrincipal;
                var sid = cprincipal.FindFirst(SanteDBClaimTypes.Sid)?.Value;
                Guid uuid = Guid.Empty;
                if (sid != null && Guid.TryParse(sid, out uuid))
                    return uuid;
            }

            String name = context.Principal.Identity.Name ?? AuthenticationContext.Current.Principal.Identity.Name;

            var securityUser = context.Connection.Table<DbSecurityUser>().Where(o => o.UserName.ToLower() == name.ToLower()).ToList().SingleOrDefault();
            if (securityUser == null)
            {
                var securityDevice = context.Connection.Table<DbSecurityDevice>().Where(o => o.PublicId.ToLower() == name.ToLower()).ToList().SingleOrDefault();
                if (securityDevice == null)
                {
                    this.m_tracer.TraceWarning("Device doesn't exist locally, using GUID.EMPTY");
                    return Guid.Empty;
                }
                return securityDevice.Key;
            }
            else
                return securityUser.Key;
        }

        /// <summary>
        /// Performthe actual insert.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        public TData Insert(SQLiteDataContext context, TData data)
        {
            var retVal = this.InsertInternal(context, data);
            //if (retVal != data) System.Diagnostics.Debugger.Break();
            context.AddCacheCommit(retVal);
            return retVal;
        }
        /// <summary>
        /// Perform the actual update.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        public TData Update(SQLiteDataContext context, TData data)
        {
            // JF- Probably no need to do this now
            // Make sure we're updating the right thing
            //if (data.Key.HasValue)
            //{
            //    var cacheItem = ApplicationContext.Current.GetService<IDataCachingService>()?.GetCacheItem(data.GetType(), data.Key.Value);
            //    if (cacheItem != null)
            //    {
            //        cacheItem.CopyObjectData(data);
            //        data = cacheItem as TData;
            //    }
            //}

            var retVal = this.UpdateInternal(context, data);
            //if (retVal != data) System.Diagnostics.Debugger.Break();
            context.AddCacheCommit(retVal);
            return retVal;

        }
        /// <summary>
        /// Performs the actual obsoletion
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        public TData Obsolete(SQLiteDataContext context, TData data)
        {
            var retVal = this.ObsoleteInternal(context, data);
            //if (retVal != data) System.Diagnostics.Debugger.Break();
            context.AddCacheCommit(retVal);
            return retVal;
        }
        /// <summary>
        /// Performs the actual query
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="query">Query.</param>
        public IEnumerable<TData> Query(SQLiteDataContext context, Expression<Func<TData, bool>> query, Guid queryId, int offset, int count, out int totalResults, bool countResults, ModelSort<TData>[] orderBy)
        {
            var retVal = this.QueryInternal(context, query, offset, count, out totalResults, queryId, countResults, orderBy);

            foreach (var i in retVal.Where(i => i != null))
                context.AddCacheCommit(i);

            return retVal;

        }
     


        /// <summary>
        /// Maps the data to a model instance
        /// </summary>
        /// <returns>The model instance.</returns>
        /// <param name="dataInstance">Data instance.</param>
        public abstract TData ToModelInstance(Object dataInstance, SQLiteDataContext context);

        /// <summary>
        /// Froms the model instance.
        /// </summary>
        /// <returns>The model instance.</returns>
        /// <param name="modelInstance">Model instance.</param>
        public abstract Object FromModelInstance(TData modelInstance, SQLiteDataContext context);

        /// <summary>
        /// Performthe actual insert.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        protected abstract TData InsertInternal(SQLiteDataContext context, TData data);
        /// <summary>
        /// Perform the actual update.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        protected abstract TData UpdateInternal(SQLiteDataContext context, TData data);
        /// <summary>
        /// Performs the actual obsoletion
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        protected abstract TData ObsoleteInternal(SQLiteDataContext context, TData data);
        /// <summary>
        /// Performs the actual query
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="query">Query.</param>
        protected abstract IEnumerable<TData> QueryInternal(SQLiteDataContext context, Expression<Func<TData, bool>> query, int offset, int count, out int totalResults, Guid queryId, bool countResults, ModelSort<TData>[] orderBy);
        /// <summary>
        /// Performs the actual query
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="query">Query.</param>
        protected abstract IEnumerable<TData> QueryInternal(SQLiteDataContext context, String storedQueryName, IDictionary<String, Object> parms, int offset, int count, out int totalResults, Guid queryId, bool countResults, ModelSort<TData>[] orderBy);

        /// <summary>
        /// Query internal without caring about limiting
        /// </summary>
        /// <param name="context"></param>
        /// <param name="expr"></param>
        /// <returns></returns>
        public IEnumerable<TData> Query(SQLiteDataContext context, Expression<Func<TData, bool>> expr)
        {
            int t;
            return this.QueryInternal(context, expr, 0, -1, out t, Guid.Empty, false, null);
        }

        /// <summary>
        /// Get the specified key.
        /// </summary>
        /// <param name="key">Key.</param>
        internal virtual TData Get(SQLiteDataContext context, Guid key)
        {
            int totalResults = 0;
            var existing = ApplicationContext.Current.GetService<IDataCachingService>().GetCacheItem(key);
            if (existing != null)
                return existing as TData;
            return this.QueryInternal(context, o => o.Key == key, 0, 1, out totalResults, Guid.Empty, false, null)?.SingleOrDefault();
        }

        /// <summary>
        /// Insert the specified object
        /// </summary>
        public virtual object Insert(object data)
        {
            return this.Insert(data as TData, TransactionMode.Commit, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Update the specified object
        /// </summary>
        public virtual object Update(object data)
        {
            return this.Update(data as TData, TransactionMode.Commit, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Obsoletes the specified data
        /// </summary>
        public virtual object Obsolete(object data)
        {
            return this.Obsolete(data as TData, TransactionMode.Commit, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Gets the specified data
        /// </summary>
        object IDataPersistenceService.Get(Guid id)
        {
            return this.Get(id, null, false, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Query the specified object
        /// </summary>
        public IEnumerable Query(Expression query, int offset, int? count, out int totalResults)
        {
            return this.Query((Expression<Func<TData, bool>>)query, null, offset, count, out totalResults, true, false, AuthenticationContext.Current.Principal, null);
        }

        /// <summary>
        /// Insert the specified data
        /// </summary>
        object ISQLitePersistenceService.Insert(SQLiteDataContext context, object data)
        {
            return this.Insert(context, (TData)data);
        }

        /// <summary>
        /// Update
        /// </summary>
        object ISQLitePersistenceService.Update(SQLiteDataContext context, object data)
        {
            return this.Update(context, (TData)data);
        }

        /// <summary>
        /// Obsolete
        /// </summary>
        object ISQLitePersistenceService.Obsolete(SQLiteDataContext context, object data)
        {
            return this.Obsolete(context, (TData)data);
        }

        /// <summary>
        /// Get the specified data
        /// </summary>
        object ISQLitePersistenceService.Get(SQLiteDataContext context, Guid id)
        {
            return this.Get(context, id);
        }

        /// <summary>
        /// To model instance
        /// </summary>
        object ISQLitePersistenceService.ToModelInstance(object domainInstance, SQLiteDataContext context)
        {
            return this.ToModelInstance(domainInstance, context);
        }

        /// <summary>
        /// Perform a stored query
        /// </summary>
        public IEnumerable<TData> Query(Expression<Func<TData, bool>> query, Guid queryId, int offset, int? count, out int totalCount, IPrincipal overrideAuthContext, params ModelSort<TData>[] orderBy)
        {
            return this.Query((Expression<Func<TData, bool>>)query, queryId, offset, count, out totalCount, true, false, AuthenticationContext.Current.Principal, orderBy);

        }
    }
}

