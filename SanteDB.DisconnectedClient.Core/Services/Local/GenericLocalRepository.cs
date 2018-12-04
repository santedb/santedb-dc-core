/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-6-22
 */
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.Core.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Represents a base class for entity repository services
    /// </summary>
    public class GenericLocalRepository<TEntity> :
        IValidatingRepositoryService<TEntity>,
        IPersistableQueryRepositoryService<TEntity>,
        IFastQueryRepositoryService<TEntity>,
        IRepositoryService<TEntity>,
        ISecuredRepositoryService
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

        /// <summary>
        /// Gets the policy required for querying
        /// </summary>
        protected virtual String QueryPolicy => PermissionPolicyIdentifiers.Login;
        /// <summary>
        /// Gets the policy required for reading
        /// </summary>
        protected virtual String ReadPolicy => PermissionPolicyIdentifiers.Login;
        /// <summary>
        /// Gets the policy required for writing
        /// </summary>
        protected virtual String WritePolicy => PermissionPolicyIdentifiers.Login;
        /// <summary>
        /// Gets the policy required for deleting
        /// </summary>
        protected virtual String DeletePolicy => PermissionPolicyIdentifiers.Login;
        /// <summary>
        /// Gets the policy for altering
        /// </summary>
        protected virtual String AlterPolicy => PermissionPolicyIdentifiers.Login;

        /// <summary>
        /// Find with stored query parameters
        /// </summary>
        public virtual IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> query, int offset, int? count, out int totalResults, Guid queryId)
        {

            // Demand permission
            this.DemandQuery();

            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException($"Unable to locate {typeof(IDataPersistenceService<TEntity>).FullName}");
            }

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            IEnumerable<TEntity> results = null;
            if(persistenceService is IStoredQueryDataPersistenceService<TEntity>)
                results = (persistenceService as IStoredQueryDataPersistenceService<TEntity>).Query(query, queryId, offset, count, out totalResults, AuthenticationContext.Current.Principal);
            else
                results = persistenceService.Query(query, offset, count, out totalResults, AuthenticationContext.Current.Principal);

            var retVal = businessRulesService != null ? businessRulesService.AfterQuery(results) : results;

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
            {
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");
            }

            data = this.Validate(data);

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();
            data = businessRulesService?.BeforeInsert(data) ?? data;
            data = persistenceService.Insert(data, TransactionMode.Commit, AuthenticationContext.Current.Principal);
            ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Insert);
            businessRulesService?.AfterInsert(data);

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
            {
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");
            }

            var entity = persistenceService.Get(key, null, false, AuthenticationContext.SystemPrincipal);

            if (entity == null)
            {
                throw new KeyNotFoundException($"Entity {key} not found");
            }

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            entity = businessRulesService?.BeforeObsolete(entity) ?? entity;
            entity = persistenceService.Obsolete(entity, TransactionMode.Commit, AuthenticationContext.Current.Principal);

            ApplicationContext.Current.GetService<IQueueManagerService>().Outbound.Enqueue(entity, SynchronizationOperationType.Obsolete);

            return businessRulesService?.AfterObsolete(entity) ?? entity;
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
            {
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");
            }

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            var result = persistenceService.Get(key, null, false, AuthenticationContext.Current.Principal);

            var retVal = businessRulesService?.AfterRetrieve(result) ?? result;
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

                data = businessRulesService?.BeforeUpdate(data) ?? data;
                data = persistenceService.Update(data, TransactionMode.Commit, AuthenticationContext.Current.Principal);

                var diff = ApplicationContext.Current.GetService<IPatchService>()?.Diff(old, this.Get(data.Key.Value), "participation");
                if (diff != null)
                    ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(diff, SynchronizationOperationType.Update);
                else
                    ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Update);


                businessRulesService?.AfterUpdate(data);
                return data;
            }
            catch (KeyNotFoundException)
            {
                data = businessRulesService?.BeforeInsert(data) ?? data;
                data = persistenceService.Insert(data, TransactionMode.Commit, AuthenticationContext.Current.Principal);
                ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Insert);
                businessRulesService?.AfterInsert(data);
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
                        bundle.Item[i] = vrsi.GetType().GetRuntimeMethod(nameof(Validate), new Type[] { typeof(TEntity) }).Invoke(vrsi, new object[] { itm }) as IdentifiedData;
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
        public virtual IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> query, int offset, int? count, out int totalResults)
        {
            return this.Find(query, offset, count, out totalResults, Guid.Empty);
        }

        /// <summary>
        /// Demand permission
        /// </summary>
        protected void Demand(String policyId)
        {
            var pdp = ApplicationContext.Current.GetService<IPolicyDecisionService>();
            var outcome = pdp?.GetPolicyOutcome(AuthenticationContext.Current.Principal, policyId);
            if (outcome != PolicyGrantType.Grant)
                throw new PolicyViolationException(AuthenticationContext.Current.Principal, policyId, outcome ?? PolicyGrantType.Deny);

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
    }
}