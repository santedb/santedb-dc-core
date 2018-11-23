﻿/*
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
 * Date: 2018-11-23
 */
using SanteDB.Core.Exceptions;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Entity repository base
    /// </summary>
    public abstract class EntityRepositoryBase : IPersistableQueryRepositoryService, IAuditEventSource, IFastQueryRepositoryService
    {
        public event EventHandler<AuditDataEventArgs> DataCreated;
        public event EventHandler<AuditDataEventArgs> DataUpdated;
        public event EventHandler<AuditDataEventArgs> DataObsoleted;
        public event EventHandler<AuditDataDisclosureEventArgs> DataDisclosed;

        /// <summary>
        /// Find with stored query parameters
        /// </summary>
        public IEnumerable<TEntity> Find<TEntity>(Expression<Func<TEntity, bool>> query, int offset, int? count, out int totalResults, Guid queryId) where TEntity : IdentifiedData
        {
            return this.Find(query, offset, count, out totalResults, queryId, false);
        }

        /// <summary>
        /// Find with stored query parameters
        /// </summary>
        public IEnumerable<TEntity> FindFast<TEntity>(Expression<Func<TEntity, bool>> query, int offset, int? count, out int totalResults, Guid queryId) where TEntity : IdentifiedData
        {
            return this.Find(query, offset, count, out totalResults, queryId, true);
        }

        /// <summary>
        /// Find with specified query 
        /// </summary>
        public IEnumerable<TEntity> Find<TEntity>(Expression<Func<TEntity, bool>> query, int offset, int? count, out int totalResults, Guid queryId, bool fastSearch) where TEntity : IdentifiedData
        {

            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException($"Unable to locate {typeof(IDataPersistenceService<TEntity>).FullName}");
            }

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            IEnumerable<TEntity> results = null;
            if(fastSearch)
                results = persistenceService.QueryFast(query, offset, count, out totalResults, queryId);
            else
                results = persistenceService.Query(query, offset, count, out totalResults, queryId);

            this.DataDisclosed?.Invoke(this, new AuditDataDisclosureEventArgs(query.ToString(), results));

            return businessRulesService != null ? businessRulesService.AfterQuery(results) : results;
        }

        /// <summary>
        /// Performs insert of object
        /// </summary>
        protected TEntity Insert<TEntity>(TEntity entity) where TEntity : IdentifiedData
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");
            }

            this.Validate(entity);

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            entity = businessRulesService?.BeforeInsert(entity) ?? entity;

            entity = persistenceService.Insert(entity);

            entity = businessRulesService?.AfterInsert(entity) ?? entity;

            // Patient relationships
            ApplicationContext.Current.GetService<IQueueManagerService>().Outbound.Enqueue(entity, SynchronizationOperationType.Insert);

            this.DataCreated?.Invoke(this, new AuditDataEventArgs(entity));

            return entity;
        }

        /// <summary>
        /// Obsolete the specified data
        /// </summary>
        protected TEntity Obsolete<TEntity>(Guid key) where TEntity : IdentifiedData
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");
            }

            var entity = persistenceService.Get(key);

            if (entity == null)
            {
                throw new InvalidOperationException("Entity Relationship not found");
            }

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            entity = businessRulesService?.BeforeObsolete(entity) ?? entity;

            entity = persistenceService.Obsolete(entity);

            entity = businessRulesService?.AfterObsolete(entity) ?? entity;

            // Patient relationships
            ApplicationContext.Current.GetService<IQueueManagerService>().Outbound.Enqueue(entity, SynchronizationOperationType.Obsolete);

            this.DataObsoleted?.Invoke(this, new AuditDataEventArgs(entity));

            return entity;
        }

        /// <summary>
        /// Get specified data from persistence
        /// </summary>
        protected TEntity Get<TEntity>(Guid key, Guid versionKey) where TEntity : IdentifiedData
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");
            }

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            var result = persistenceService.Get(key);

			if (result != null)
				result = businessRulesService?.AfterRetrieve(result) ?? result;

            this.DataDisclosed?.Invoke(this, new AuditDataDisclosureEventArgs(key.ToString(), new object[] { result }));
            return result;
        }

        /// <summary>
        /// Save the specified entity (insert or update)
        /// </summary>
        protected TEntity Save<TEntity>(TEntity data) where TEntity : IdentifiedData
        {
            
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TEntity>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<TEntity>)}");
            }

            this.Validate(data);

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            try
            {
                TEntity old = null;

                if (data.Key.HasValue)
                {
                    old = persistenceService.Get(data.Key.Value)?.Clone() as TEntity;
                    if (old is Entity)
                        old = (TEntity)(old as Entity)?.Copy();
                }

                // HACK: Lookup by ER src<>trg
                if (old == null && typeof(TEntity) == typeof(EntityRelationship))
                {
                    var tr = 0;
                    var erd = data as EntityRelationship;
                    old = (TEntity)(persistenceService as IDataPersistenceService<EntityRelationship>).Query(o => o.SourceEntityKey == erd.SourceEntityKey && o.TargetEntityKey == erd.TargetEntityKey, 0, 1, out tr, Guid.Empty).OfType<Object>().FirstOrDefault();
                }

                data = businessRulesService?.BeforeUpdate(data) ?? data;
                data = persistenceService.Update(data);
                data = businessRulesService?.AfterUpdate(data) ?? data;


                var diff = ApplicationContext.Current.GetService<IPatchService>().Diff(old, this.Get<TEntity>(data.Key.Value, Guid.Empty), "participation");

                ApplicationContext.Current.GetService<IQueueManagerService>().Outbound.Enqueue(diff, SynchronizationOperationType.Update);

                this.DataUpdated?.Invoke(this, new AuditDataEventArgs(data));
            }
            catch (DataPersistenceException)
            {
                data = businessRulesService?.BeforeInsert(data) ?? data;
                data = persistenceService.Insert(data);
                data = businessRulesService?.AfterInsert(data) ?? data;

                // Patient relationships
                if ((data as Entity)?.Relationships.Count > 0 || (data as Entity)?.Participations.Count > 0)
                    ApplicationContext.Current.GetService<IQueueManagerService>().Outbound.Enqueue(Bundle.CreateBundle(data), SynchronizationOperationType.Insert);
                else
                    ApplicationContext.Current.GetService<IQueueManagerService>().Outbound.Enqueue(data, SynchronizationOperationType.Insert);

                this.DataCreated?.Invoke(this, new AuditDataEventArgs(data));
            }

            return data;
        }

        /// <summary>
        /// Validate a patient before saving
        /// </summary>
        internal TEntity Validate<TEntity>(TEntity p) where TEntity : IdentifiedData
        {
            p = (TEntity)p.Clean(); // clean up messy data

            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<TEntity>>();

            var details = businessRulesService?.Validate(p) ?? new List<DetectedIssue>();

            if (details.Any(d => d.Priority == DetectedIssuePriorityType.Error))
            {
                throw new DetectedIssueException(details);
            }
            return p;
        }
    }
}