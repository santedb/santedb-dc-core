/*
 * Based on OpenIZ, Copyright (C) 2015 - 2020 Mohawk College of Applied Arts and Technology
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
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Services.Local
{
    /// <summary>
    /// Represents a base class for entity repository services
    /// </summary>
    public class GenericLocalRepository<TEntity> :
        IRepositoryService,
        IValidatingRepositoryService<TEntity>,
        IPersistableQueryRepositoryService<TEntity>,
        IFastQueryRepositoryService<TEntity>,
        IRepositoryService<TEntity>,
        ISecuredRepositoryService,
        INotifyRepositoryService<TEntity>
        where TEntity : IdentifiedData
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => $"Local Data Storage Repository for {typeof(TEntity).FullName}";


        /// <summary>
        /// Trace source
        /// </summary>
        protected Tracer m_traceSource = Tracer.GetTracer(typeof(GenericLocalRepository<TEntity>));

        public event EventHandler<DataPersistingEventArgs<TEntity>> Inserting;
        public event EventHandler<DataPersistedEventArgs<TEntity>> Inserted;
        public event EventHandler<DataPersistingEventArgs<TEntity>> Saving;
        public event EventHandler<DataPersistedEventArgs<TEntity>> Saved;
        public event EventHandler<DataPersistingEventArgs<TEntity>> Obsoleting;
        public event EventHandler<DataPersistedEventArgs<TEntity>> Obsoleted;
        public event EventHandler<DataRetrievingEventArgs<TEntity>> Retrieving;
        public event EventHandler<DataRetrievedEventArgs<TEntity>> Retrieved;
        public event EventHandler<QueryRequestEventArgs<TEntity>> Querying;
        public event EventHandler<QueryResultEventArgs<TEntity>> Queried;



        /// <summary>
        /// Gets the policy required for querying
        /// </summary>
        protected virtual String QueryPolicy => PermissionPolicyIdentifiers.LoginAsService;
        /// <summary>
        /// Gets the policy required for reading
        /// </summary>
        protected virtual String ReadPolicy => PermissionPolicyIdentifiers.LoginAsService;
        /// <summary>
        /// Gets the policy required for writing
        /// </summary>
        protected virtual String WritePolicy => PermissionPolicyIdentifiers.LoginAsService;
        /// <summary>
        /// Gets the policy required for deleting
        /// </summary>
        protected virtual String DeletePolicy => PermissionPolicyIdentifiers.LoginAsService;
        /// <summary>
        /// Gets the policy for altering
        /// </summary>
        protected virtual String AlterPolicy => PermissionPolicyIdentifiers.LoginAsService;

        /// <summary>
        /// Find with stored query parameters
        /// </summary>
        public virtual IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> query, int offset, int? count, out int totalResults, Guid queryId, params ModelSort<TEntity>[] orderBy)
        {

            // Demand permission
            this.DemandQuery();

            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();
            if (persistenceService == null)
                throw new InvalidOperationException($"Unable to locate {typeof(IDataPersistenceService<TEntity>).FullName}");


            // Fire pre event
            var preEvtArgs = new QueryRequestEventArgs<TEntity>(query, offset, count, queryId, AuthenticationContext.Current.Principal);
            this.Querying?.Invoke(this, preEvtArgs);
            if(preEvtArgs.Cancel)
            {
                this.m_traceSource.TraceWarning("Pre-query event indicates cancel");
                totalResults = preEvtArgs.TotalResults;
                return preEvtArgs.Results;
            }

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();
            IEnumerable<TEntity> results = null;
            if(persistenceService is IStoredQueryDataPersistenceService<TEntity>)
                results = (persistenceService as IStoredQueryDataPersistenceService<TEntity>).Query(query, queryId, offset, count, out totalResults, AuthenticationContext.Current.Principal, orderBy);
            else
                results = persistenceService.Query(query, offset, count, out totalResults, AuthenticationContext.Current.Principal, orderBy);

            var retVal = businessRulesService != null ? businessRulesService.AfterQuery(results) : results;
            
            this.Queried?.Invoke(this, new QueryResultEventArgs<TEntity>(query, results, offset, count, totalResults, queryId, AuthenticationContext.AnonymousPrincipal));
            
            return retVal;
        }

        /// <summary>
        /// Performs insert of object
        /// </summary>
        public virtual TEntity Insert(TEntity data)
        {
            // Demand permission
            this.DemandWrite(data);

            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");

            var preEvent = new DataPersistingEventArgs<TEntity>(data, AuthenticationContext.Current.Principal);
            this.Inserting?.Invoke(this, preEvent);
            if(preEvent.Cancel)
            {
                this.m_traceSource.TraceWarning("Pre-persistence event indicates cancel");
                return preEvent.Data;
            }
            data = preEvent.Data;

            data = this.Validate(data);

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();
            data = businessRulesService?.BeforeInsert(data) ?? data;
            data = persistenceService.Insert(data, TransactionMode.Commit, AuthenticationContext.Current.Principal);
            ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Insert);
            data = businessRulesService?.AfterInsert(data) ?? data;
            this.Inserted?.Invoke(this, new DataPersistedEventArgs<TEntity>(data, AuthenticationContext.Current.Principal));
            return data;
        }

        /// <summary>
        /// Obsolete the specified data
        /// </summary>
        public virtual TEntity Obsolete(Guid key)
        {
            // Demand permission
            this.DemandDelete(key);

            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");


            var entity = persistenceService.Get(key, null, false, AuthenticationContext.SystemPrincipal);

            if (entity == null)
                throw new KeyNotFoundException($"Entity {key} not found");

            var preEvent = new DataPersistingEventArgs<TEntity>(entity, AuthenticationContext.Current.Principal);
            this.Obsoleting?.Invoke(this, preEvent);
            if (preEvent.Cancel)
            {
                this.m_traceSource.TraceWarning("Pre-persistence event indicates cancel");
                return preEvent.Data;
            }
            entity = preEvent.Data;

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            entity = businessRulesService?.BeforeObsolete(entity) ?? entity;
            entity = persistenceService.Obsolete(entity, TransactionMode.Commit, AuthenticationContext.Current.Principal);

            ApplicationContext.Current.GetService<IQueueManagerService>().Outbound.Enqueue(entity, SynchronizationOperationType.Obsolete);

            entity = businessRulesService?.AfterObsolete(entity) ?? entity;
            this.Obsoleted?.Invoke(this, new DataPersistedEventArgs<TEntity>(entity, AuthenticationContext.Current.Principal));
            return entity;
        }

        /// <summary>
        /// Get the specified key
        /// </summary>
        public virtual TEntity Get(Guid key)
        {
            return this.Get(key, Guid.Empty);
        }

        /// <summary>
        /// Get specified data from persistence
        /// </summary>
        public virtual TEntity Get(Guid key, Guid versionKey)
        {
            // Demand permission
            this.DemandRead(key);
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");

            var preEventArg = new DataRetrievingEventArgs<TEntity>(key, versionKey, AuthenticationContext.Current.Principal);
            this.Retrieving?.Invoke(this, preEventArg);
            if(preEventArg.Cancel)
            {
                this.m_traceSource.TraceWarning("Pre-retrieve event indicates cancel");
                return preEventArg.Result;
            }
            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();
            var result = persistenceService.Get(key, null, false, AuthenticationContext.Current.Principal);
            var retVal = businessRulesService?.AfterRetrieve(result) ?? result;
            this.Retrieved?.Invoke(this, new DataRetrievedEventArgs<TEntity>(retVal, AuthenticationContext.Current.Principal));
            return retVal;
        }

        /// <summary>
        /// Save the specified entity (insert or update)
        /// </summary>
        public virtual TEntity Save(TEntity data)
        {
            // Demand permission
            this.DemandAlter(data);

            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");
            }

            data = this.Validate(data);

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            try
            {
                var preEvent = new DataPersistingEventArgs<TEntity>(data, AuthenticationContext.Current.Principal);
                this.Saving?.Invoke(this, preEvent);
                if (preEvent.Cancel)
                {
                    this.m_traceSource.TraceWarning("Pre-persistence event indicates cancel");
                    return preEvent.Data;
                }
                data = preEvent.Data;

                // We need to know what the old version looked like so we can patch it
                TEntity old = null;

                // Data key
                if (data.Key.HasValue)
                {
                    old = persistenceService.Get(data.Key.Value, null, false, AuthenticationContext.SystemPrincipal) as TEntity;
                    if (old is Entity)
                        old = (TEntity)(old as Entity)?.Copy();
                    else if (old is Act)
                        old = (TEntity)(old as Act)?.Copy();
                }

                // HACK: Lookup by ER src<>trg
                if (old == null && typeof(TEntity) == typeof(EntityRelationship))
                {
                    var tr = 0;
                    var erd = data as EntityRelationship;
                    old = (TEntity)(persistenceService as IDataPersistenceService<EntityRelationship>).Query(o => o.SourceEntityKey == erd.SourceEntityKey && o.TargetEntityKey == erd.TargetEntityKey, 0, 1, out tr, AuthenticationContext.SystemPrincipal).OfType<EntityRelationship>().FirstOrDefault().Clone();
                }

                // Old does not exist
                if (old == null)
                {
                    data = businessRulesService?.BeforeInsert(data) ?? data;
                    data = persistenceService.Insert(data, TransactionMode.Commit, AuthenticationContext.Current.Principal);
                    ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Insert);
                    data = businessRulesService?.AfterInsert(data) ?? data;
                    this.Saved?.Invoke(this, new DataPersistedEventArgs<TEntity>(data, AuthenticationContext.Current.Principal));
                }
                else
                {
                    data = businessRulesService?.BeforeUpdate(data) ?? data;
                    data = persistenceService.Update(data, TransactionMode.Commit, AuthenticationContext.Current.Principal);

                    // Use patches
                    if (ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>().UsePatches)
                    {
                        var diff = ApplicationContext.Current.GetService<IPatchService>()?.Diff(old, this.Get(data.Key.Value), "participation");
                        if (diff != null)
                            ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(diff, SynchronizationOperationType.Update);
                        else
                            ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Update);
                    }
                    else
                        ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Update);


                    data = businessRulesService?.AfterUpdate(data) ?? data;
                    this.Saved?.Invoke(this, new DataPersistedEventArgs<TEntity>(data, AuthenticationContext.Current.Principal));
                }
                
                return data;
            }
            catch (KeyNotFoundException)
            {
                data = businessRulesService?.BeforeInsert(data) ?? data;
                data = persistenceService.Insert(data, TransactionMode.Commit, AuthenticationContext.Current.Principal);
                ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Insert);
                data = businessRulesService?.AfterInsert(data) ?? data;
                this.Saved?.Invoke(this, new DataPersistedEventArgs<TEntity>(data, AuthenticationContext.Current.Principal));
                return data;
            }
        }

        /// <summary>
        /// Validate a patient before saving
        /// </summary>
        public virtual TEntity Validate(TEntity p)
        {
            p = (TEntity)p.Clean(); // clean up messy data

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            var details = businessRulesService?.Validate(p) ?? new List<DetectedIssue>();

            if (details.Any(d => d.Priority == DetectedIssuePriorityType.Error))
            {
                throw new DetectedIssueException(details);
            }

            // Bundles cascade
            var bundle = p as Bundle;
            if (bundle != null)
            {
                for (int i = 0; i < bundle.Item.Count; i++)
                {
                    var itm = bundle.Item[i];
                    var vrst = typeof(IValidatingRepositoryService<>).MakeGenericType(itm.GetType());
                    var vrsi = ApplicationContext.Current.GetService(vrst);

                    if (vrsi != null)
                        bundle.Item[i] = vrsi.GetType().GetRuntimeMethod(nameof(Validate), new Type[] { itm.GetType() }).Invoke(vrsi, new object[] { itm }) as IdentifiedData;
                }
            }
            return p;
        }

        /// <summary>
        /// Perform a faster version of the query for an object
        /// </summary>
        public virtual IEnumerable<TEntity> FindFast(Expression<Func<TEntity, bool>> query, int offset, int? count, out int totalResults, Guid queryId)
        {
            return this.Find(query, offset, count, out totalResults, queryId);
        }

        /// <summary>
        /// Perform a simple find
        /// </summary>
        public virtual IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> query)
        {
            int t = 0;
            return this.Find(query, 0, null, out t, Guid.Empty);
        }

        /// <summary>
        /// Perform a normal find
        /// </summary>
        public virtual IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> query, int offset, int? count, out int totalResults, params ModelSort<TEntity>[] orderBy)
        {
            return this.Find(query, offset, count, out totalResults, Guid.Empty, orderBy);
        }

        /// <summary>
        /// Demand permission
        /// </summary>
        protected void Demand(String policyId)
        {
            ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(policyId);
        }

        /// <summary>
        /// Demand write permission
        /// </summary>
        public virtual void DemandWrite(object data)
        {
            this.Demand(this.WritePolicy);
        }

        /// <summary>
        /// Demand read
        /// </summary>
        /// <param name="key"></param>
        public virtual void DemandRead(Guid key)
        {
            this.Demand(this.ReadPolicy);
        }

        /// <summary>
        /// Demand delete permission
        /// </summary>
        public virtual void DemandDelete(Guid key)
        {
            this.Demand(this.DeletePolicy);
        }

        /// <summary>
        /// Demand alter permission
        /// </summary>
        public virtual void DemandAlter(object data)
        {
            this.Demand(this.AlterPolicy);
        }

        /// <summary>
        /// Demand query 
        /// </summary>
        public virtual void DemandQuery()
        {
            this.Demand(this.QueryPolicy);
        }
        /// <summary>
        /// Get the specified data
        /// </summary>
        IdentifiedData IRepositoryService.Get(Guid key)
        {
            return this.Get(key);
        }

        /// <summary>
        /// Find specified data
        /// </summary>
        IEnumerable<IdentifiedData> IRepositoryService.Find(Expression query)
        {
            return this.Find((Expression<Func<TEntity, bool>>)query).OfType<IdentifiedData>();
        }

        /// <summary>
        /// Find specified data
        /// </summary>
        IEnumerable<IdentifiedData> IRepositoryService.Find(Expression query, int offset, int? count, out int totalResults)
        {
            return this.Find((Expression<Func<TEntity, bool>>)query, offset, count, out totalResults).OfType<IdentifiedData>();
        }

        /// <summary>
        /// Insert the specified data
        /// </summary>
        IdentifiedData IRepositoryService.Insert(object data)
        {
            return this.Insert((TEntity)data);
        }

        /// <summary>
        /// Save specified data
        /// </summary>
        IdentifiedData IRepositoryService.Save(object data)
        {
            return this.Save((TEntity)data);
        }

        /// <summary>
        /// Obsolete
        /// </summary>
        IdentifiedData IRepositoryService.Obsolete(Guid key)
        {
            return this.Obsolete(key);
        }
    }
}