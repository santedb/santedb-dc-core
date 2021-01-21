/*
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
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.DataType;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Extensibility;
using SanteDB.DisconnectedClient.SQLite.Model.Roles;
using SQLite.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Entity persistence service
    /// </summary>
    public class EntityPersistenceService : VersionedDataPersistenceService<Entity, DbEntity>
    {
        
        /// <summary>
        /// To model instance
        /// </summary>
        public virtual TEntityType ToModelInstance<TEntityType>(DbEntity dbInstance, SQLiteDataContext context) where TEntityType : Entity, new()
        {
            var retVal = m_mapper.MapDomainInstance<DbEntity, TEntityType>(dbInstance, useCache: !context.Connection.IsInTransaction);

            // Has this been updated? If so, minimal information about the previous version is available
            if (dbInstance.UpdatedTime != null)
            {
                retVal.CreationTime = (DateTimeOffset)dbInstance.UpdatedTime;
                retVal.CreatedByKey = dbInstance.UpdatedByKey;

                // HACK: We set the previous version because this is non versioned 
                retVal.SetPreviousVersion(new Entity()
                {
                    ClassConcept = retVal.ClassConcept,
                    DeterminerConcept = retVal.DeterminerConcept,
                    Key = dbInstance.Key,
                    VersionKey = dbInstance.PreviousVersionKey,
                    CreationTime = (DateTimeOffset)dbInstance.CreationTime,
                    CreatedByKey = dbInstance.CreatedByKey
                });
            }


            retVal.LoadAssociations(context,
                // Exclude
                nameof(SanteDB.Core.Model.Entities.Entity.Participations),
                nameof(SanteDB.Core.Model.Entities.UserEntity.SecurityUser)
                );

            //if (!loadFast)
            //{
            //    foreach (var itm in retVal.Relationships.Where(o => !o.InversionIndicator && o.TargetEntity == null))
            //        itm.TargetEntity = this.CacheConvert(context.Get<DbEntity>(itm.TargetEntityKey.Value.ToByteArray()), context, true);
            //    retVal.Relationships.RemoveAll(o => o.InversionIndicator);
            //    retVal.Relationships.AddRange(
            //        context.Table<DbEntityRelationship>().Where(o => o.TargetUuid == dbInstance.Uuid).ToList().Select(o => new EntityRelationship(new Guid(o.RelationshipTypeUuid), new Guid(o.TargetUuid))
            //        {
            //            SourceEntityKey = new Guid(o.EntityUuid),
            //            InversionIndicator = true
            //        })
            //    );
            //    retVal.Participations = new List<ActParticipation>(context.Table<DbActParticipation>().Where(o => o.EntityUuid == dbInstance.Uuid).ToList().Select(o => new ActParticipation(new Guid(o.ParticipationRoleUuid), retVal)
            //    {
            //        ActKey = new Guid(o.ActUuid),
            //        Key = o.Key
            //    }));
            //}


            return retVal;
        }

        /// <summary>
        /// Create an appropriate entity based on the class code
        /// </summary>
        public override Entity ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            // Alright first, which type am I mapping to?
            var dbEntity = dataInstance as DbEntity;
            if (dbEntity != null)
                switch (new Guid(dbEntity.ClassConceptUuid).ToString().ToUpper())
                {
                    case EntityClassKeyStrings.Device:
                        return new DeviceEntityPersistenceService().ToModelInstance(dataInstance, context);
                    case EntityClassKeyStrings.NonLivingSubject:
                        return new ApplicationEntityPersistenceService().ToModelInstance(dataInstance, context);
                    case EntityClassKeyStrings.Person:
                        return new PersonPersistenceService().ToModelInstance(dataInstance, context);
                    case EntityClassKeyStrings.Patient:
                        return new PatientPersistenceService().ToModelInstance(dataInstance, context);
                    case EntityClassKeyStrings.Provider:
                        return new ProviderPersistenceService().ToModelInstance(dataInstance, context);
                    case EntityClassKeyStrings.Place:
                    case EntityClassKeyStrings.CityOrTown:
                    case EntityClassKeyStrings.Country:
                    case EntityClassKeyStrings.CountyOrParish:
                    case EntityClassKeyStrings.PrecinctOrBurrogh:
                    case EntityClassKeyStrings.State:
                    case EntityClassKeyStrings.ServiceDeliveryLocation:
                        return new PlacePersistenceService().ToModelInstance(dataInstance, context);
                    case EntityClassKeyStrings.Organization:
                        return new OrganizationPersistenceService().ToModelInstance(dataInstance, context);
                    case EntityClassKeyStrings.Material:
                        return new MaterialPersistenceService().ToModelInstance(dataInstance, context);
                    case EntityClassKeyStrings.ManufacturedMaterial:
                        return new ManufacturedMaterialPersistenceService().ToModelInstance(dataInstance, context);
                    default:
                        return this.ToModelInstance<Entity>(dbEntity, context);

                }
            else if (dataInstance is DbDeviceEntity)
                return new DeviceEntityPersistenceService().ToModelInstance(dataInstance, context);
            else if (dataInstance is DbApplicationEntity)
                return new ApplicationEntityPersistenceService().ToModelInstance(dataInstance, context);
            else if (dataInstance is DbPerson)
                return new PersonPersistenceService().ToModelInstance(dataInstance, context);
            else if (dataInstance is DbPatient)
                return new PatientPersistenceService().ToModelInstance(dataInstance, context);
            else if (dataInstance is DbProvider)
                return new ProviderPersistenceService().ToModelInstance(dataInstance, context);
            else if (dataInstance is DbPlace)
                return new PlacePersistenceService().ToModelInstance(dataInstance, context);
            else if (dataInstance is DbOrganization)
                return new OrganizationPersistenceService().ToModelInstance(dataInstance, context);
            else if (dataInstance is DbMaterial)
                return new MaterialPersistenceService().ToModelInstance(dataInstance, context);
            else if (dataInstance is DbManufacturedMaterial)
                return new ManufacturedMaterialPersistenceService().ToModelInstance(dataInstance, context);
            else
                return null;

        }

        /// <summary>
        /// Convert entity into a dbentity
        /// </summary>
        public override object FromModelInstance(Entity modelInstance, SQLiteDataContext context)
        {
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbEntity()
            {
                ClassConceptUuid = modelInstance.ClassConceptKey?.ToByteArray(),
                CreatedByUuid = modelInstance.CreatedByKey?.ToByteArray(),
                CreationTime = modelInstance.CreationTime,
                DeterminerConceptUuid = modelInstance.DeterminerConceptKey?.ToByteArray(),
                ObsoletedByUuid = modelInstance.ObsoletedByKey?.ToByteArray(),
                ObsoletionTime = modelInstance.ObsoletionTime,
                PreviousVersionUuid = modelInstance.PreviousVersionKey?.ToByteArray(),
                StatusConceptUuid = modelInstance.StatusConceptKey?.ToByteArray(),
                TemplateUuid = modelInstance.TemplateKey?.ToByteArray(),
                TypeConceptUuid = modelInstance.TypeConceptKey?.ToByteArray(),
                Uuid = modelInstance.Key?.ToByteArray(),
                VersionSequenceId = (int)modelInstance.VersionSequence.GetValueOrDefault(),
                VersionUuid = modelInstance.VersionKey?.ToByteArray()
            };
        }

        /// <summary>
        /// Conversion based on type
        /// </summary>
        protected override Entity CacheConvert(DbIdentified dataInstance, SQLiteDataContext context)
        {
            return this.DoCacheConvert(dataInstance, context);
        }

        /// <summary>
        /// Perform the cache convert
        /// </summary>
        internal Entity DoCacheConvert(DbIdentified dataInstance, SQLiteDataContext context)
        {
            if (dataInstance == null)
                return null;
            // Alright first, which type am I mapping to?
            var dbEntity = dataInstance as DbEntity;
            Entity retVal = null;
            IDataCachingService cache = ApplicationContext.Current.GetService<IDataCachingService>();

            if (dbEntity != null)
                switch (new Guid(dbEntity.ClassConceptUuid).ToString().ToUpper())
                {
                    case EntityClassKeyStrings.Device:
                        retVal = cache?.GetCacheItem<DeviceEntity>(dbEntity.Key);
                        break;
                    case EntityClassKeyStrings.NonLivingSubject:
                        retVal = cache?.GetCacheItem<ApplicationEntity>(dbEntity.Key);
                        break;
                    case EntityClassKeyStrings.Person:
                        retVal = cache?.GetCacheItem<UserEntity>(dbEntity.Key);
                        if (retVal == null)
                            retVal = cache?.GetCacheItem<Person>(dbEntity.Key);
                        break;
                    case EntityClassKeyStrings.Patient:
                        retVal = cache?.GetCacheItem<Patient>(dbEntity.Key);
                        break;
                    case EntityClassKeyStrings.Provider:
                        retVal = cache?.GetCacheItem<Provider>(dbEntity.Key);

                        break;
                    case EntityClassKeyStrings.Place:
                    case EntityClassKeyStrings.CityOrTown:
                    case EntityClassKeyStrings.Country:
                    case EntityClassKeyStrings.CountyOrParish:
                    case EntityClassKeyStrings.State:
                    case EntityClassKeyStrings.ServiceDeliveryLocation:
                    case EntityClassKeyStrings.PrecinctOrBurrogh:
                        retVal = cache?.GetCacheItem<Place>(dbEntity.Key);

                        break;
                    case EntityClassKeyStrings.Organization:
                        retVal = cache?.GetCacheItem<Organization>(dbEntity.Key);

                        break;
                    case EntityClassKeyStrings.Material:
                        retVal = cache?.GetCacheItem<Material>(dbEntity.Key);

                        break;
                    case EntityClassKeyStrings.ManufacturedMaterial:
                        retVal = cache?.GetCacheItem<ManufacturedMaterial>(dbEntity.Key);

                        break;
                    default:
                        retVal = cache?.GetCacheItem<Entity>(dbEntity.Key);
                        break;
                }
            else if (dataInstance is DbDeviceEntity)
                retVal = cache?.GetCacheItem<DeviceEntity>(dataInstance.Key);
            else if (dataInstance is DbApplicationEntity)
                retVal = cache?.GetCacheItem<ApplicationEntity>(dataInstance.Key);
            else if (dataInstance is DbPerson)
                retVal = cache?.GetCacheItem<UserEntity>(dataInstance.Key);
            else if (dataInstance is DbPatient)
                retVal = cache?.GetCacheItem<Patient>(dataInstance.Key);
            else if (dataInstance is DbProvider)
                retVal = cache?.GetCacheItem<Provider>(dataInstance.Key);
            else if (dataInstance is DbPlace)
                retVal = cache?.GetCacheItem<Place>(dataInstance.Key);
            else if (dataInstance is DbOrganization)
                retVal = cache?.GetCacheItem<Organization>(dataInstance.Key);
            else if (dataInstance is DbMaterial)
                retVal = cache?.GetCacheItem<Material>(dataInstance.Key);
            else if (dataInstance is DbManufacturedMaterial)
                retVal = cache?.GetCacheItem<ManufacturedMaterial>(dataInstance.Key);

            // Return cache value
            if (retVal != null)
            {
                if (retVal.LoadState < context.DelayLoadMode)
                    retVal.LoadAssociations(context,
                        // Exclude
                        nameof(SanteDB.Core.Model.Entities.Entity.Extensions),
                        nameof(SanteDB.Core.Model.Entities.Entity.Notes),
                        nameof(SanteDB.Core.Model.Entities.Entity.Participations),
                        nameof(SanteDB.Core.Model.Entities.Entity.Telecoms),
                        nameof(SanteDB.Core.Model.Entities.UserEntity.SecurityUser)
                        );
                return retVal;
            }
            else
                return base.CacheConvert(dataInstance, context);
        }

        /// <summary>
        /// Insert the specified entity into the data context
        /// </summary>
        internal Entity InsertCoreProperties(SQLiteDataContext context, Entity data)
        {

            // Ensure FK exists
            if (data.ClassConcept != null) data.ClassConcept = data.ClassConcept.EnsureExists(context);
            if (data.DeterminerConcept != null) data.DeterminerConcept = data.DeterminerConcept.EnsureExists(context);
            if (data.StatusConcept != null) data.StatusConcept = data.StatusConcept.EnsureExists(context);
            if (data.TypeConcept != null) data.TypeConcept = data.TypeConcept.EnsureExists(context);
            if (data.Template != null) data.Template = data.Template.EnsureExists(context);

            data.ClassConceptKey = data.ClassConcept?.Key ?? data.ClassConceptKey;
            data.DeterminerConceptKey = data.DeterminerConcept?.Key ?? data.DeterminerConceptKey;
            data.StatusConceptKey = data.StatusConcept?.Key ?? data.StatusConceptKey;
            data.TypeConceptKey = data.TypeConcept?.Key ?? data.TypeConceptKey;
            data.TemplateKey = data.Template?.Key ?? data.TemplateKey;
            data.StatusConceptKey = data.StatusConceptKey.GetValueOrDefault() == Guid.Empty ? StatusKeys.New : data.StatusConceptKey;

            var retVal = base.InsertInternal(context, data);
            var altKeys = data.Tags.FirstOrDefault(o => o.TagKey == "$alt.keys")?.Value;

            // Identifiers
            if (data.Identifiers != null)
            {

                base.UpdateAssociatedItems<EntityIdentifier, Entity>(
                    new List<EntityIdentifier>(),
                    data.Identifiers,
                    retVal.Key,
                    context);

            }

            // Relationships
            if (data.Relationships != null)
            {
                data.Relationships.RemoveAll(o => o.IsEmpty());
                base.UpdateAssociatedItems<EntityRelationship, Entity>(
                    new List<EntityRelationship>(),
                    data.Relationships.Where(o => o.SourceEntityKey == null || o.SourceEntityKey == data.Key || o.TargetEntityKey == data.Key || !o.TargetEntityKey.HasValue).Distinct(new EntityRelationshipPersistenceService.Comparer()).ToList(),
                    retVal.Key,
                    context);
            }

            // Telecoms
            if (data.Telecoms != null)
                base.UpdateAssociatedItems<EntityTelecomAddress, Entity>(
                    new List<EntityTelecomAddress>(),
                    data.Telecoms,
                    retVal.Key,
                    context);

            // Extensions
            if (data.Extensions != null)
                base.UpdateAssociatedItems<EntityExtension, Entity>(
                    new List<EntityExtension>(),
                    data.Extensions,
                    retVal.Key,
                    context);

            // Names
            if (data.Names != null)
                base.UpdateAssociatedItems<EntityName, Entity>(
                    new List<EntityName>(),
                    data.Names,
                    retVal.Key,
                    context);

            // Addresses
            if (data.Addresses != null)
                base.UpdateAssociatedItems<EntityAddress, Entity>(
                    new List<EntityAddress>(),
                    data.Addresses,
                    retVal.Key,
                    context);

            // Notes
            if (data.Notes != null)
                base.UpdateAssociatedItems<EntityNote, Entity>(
                    new List<EntityNote>(),
                    data.Notes,
                    retVal.Key,
                    context);

            // Tags
            if (data.Tags != null)
                base.UpdateAssociatedItems<EntityTag, Entity>(
                    new List<EntityTag>(),
                    data.Tags.Where(o => !o.TagKey.StartsWith("$")),
                    retVal.Key,
                    context);

            
            // Participations = The source is not the patient so we don't touch
            //if (data.Participations != null)
            //    foreach (var itm in data.Participations)
            //    {
            //        itm.PlayerEntityKey = retVal.Key;
            //        itm.EnsureExists(context);
            //    }
            return retVal;
        }

        /// <summary>
        /// Update the specified entity
        /// </summary>
        internal Entity UpdateCoreProperties(SQLiteDataContext context, Entity data)
        {
            // Esnure exists
            if (data.ClassConcept != null) data.ClassConcept = data.ClassConcept.EnsureExists(context);
            if (data.DeterminerConcept != null) data.DeterminerConcept = data.DeterminerConcept.EnsureExists(context);
            if (data.StatusConcept != null) data.StatusConcept = data.StatusConcept.EnsureExists(context);
            if (data.TypeConcept != null) data.TypeConcept = data.TypeConcept.EnsureExists(context);
            data.ClassConceptKey = data.ClassConcept?.Key ?? data.ClassConceptKey;
            data.DeterminerConceptKey = data.DeterminerConcept?.Key ?? data.DeterminerConceptKey;
            data.StatusConceptKey = data.StatusConcept?.Key ?? data.StatusConceptKey;
            data.TypeConceptKey = data.TypeConcept?.Key ?? data.TypeConceptKey;

            var retVal = base.UpdateInternal(context, data);

            byte[] entityUuid = retVal.Key.Value.ToByteArray();


            // Set appropriate versioning 
            retVal.SetPreviousVersion(new Entity()
            {
                ClassConcept = retVal.ClassConcept,
                Key = retVal.Key,
                VersionKey = retVal.PreviousVersionKey,
                CreationTime = (DateTimeOffset)retVal.CreationTime,
                CreatedByKey = retVal.CreatedByKey
            });
            retVal.CreationTime = DateTimeOffset.Now;
            retVal.CreatedByKey = data.CreatedByKey == Guid.Empty || data.CreatedByKey == null ? base.CurrentUserUuid(context) : data.CreatedByKey;

            var altKeys = data.Tags?.FirstOrDefault(o => o.TagKey == "$alt.keys")?.Value;

            // Identifiers
            if (data.Identifiers != null)
            {
               
                base.UpdateAssociatedItems<EntityIdentifier, Entity>(
                    context.Connection.Table<DbEntityIdentifier>().Where(o => o.SourceUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbEntityIdentifier, EntityIdentifier>(o)).ToList(),
                    data.Identifiers,
                    retVal.Key,
                    context);

            }

            // Relationships
            if (data.Relationships != null)
            {
                data.Relationships.RemoveAll(o => o.IsEmpty());

                base.UpdateAssociatedItems<EntityRelationship, Entity>(
                    context.Connection.Table<DbEntityRelationship>().Where(o => o.SourceUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbEntityRelationship, EntityRelationship>(o)).ToList(),
                    data.Relationships.Where(o => o.SourceEntityKey == null || o.SourceEntityKey == data.Key || o.TargetEntityKey == data.Key || !o.TargetEntityKey.HasValue).Distinct(new EntityRelationshipPersistenceService.Comparer()).ToList(),
                    retVal.Key,
                    context);
            }

            // Telecoms
            if (data.Telecoms != null)
                base.UpdateAssociatedItems<EntityTelecomAddress, Entity>(
                    context.Connection.Table<DbTelecomAddress>().Where(o => o.SourceUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbTelecomAddress, EntityTelecomAddress>(o)).ToList(),
                    data.Telecoms,
                    retVal.Key,
                    context);

            // Extensions
            if (data.Extensions != null)
                base.UpdateAssociatedItems<EntityExtension, Entity>(
                    context.Connection.Table<DbEntityExtension>().Where(o => o.SourceUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbEntityExtension, EntityExtension>(o)).ToList(),
                    data.Extensions,
                    retVal.Key,
                    context);

            // Names
            if (data.Names != null)
                base.UpdateAssociatedItems<EntityName, Entity>(
                    context.Connection.Table<DbEntityName>().Where(o => o.SourceUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbEntityName, EntityName>(o)).ToList(),
                    data.Names,
                    retVal.Key,
                    context);

            // Addresses
            if (data.Addresses != null)
                base.UpdateAssociatedItems<EntityAddress, Entity>(
                    context.Connection.Table<DbEntityAddress>().Where(o => o.SourceUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbEntityAddress, EntityAddress>(o)).ToList(),
                    data.Addresses,
                    retVal.Key,
                    context);

            // Notes
            if (data.Notes != null)
                base.UpdateAssociatedItems<EntityNote, Entity>(
                    context.Connection.Table<DbEntityNote>().Where(o => o.EntityUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbEntityNote, EntityNote>(o)).ToList(),
                    data.Notes,
                    retVal.Key,
                    context);

            // Tags
            if (data.Tags != null)
                base.UpdateAssociatedItems<EntityTag, Entity>(
                    context.Connection.Table<DbEntityTag>().Where(o => o.SourceUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbEntityTag, EntityTag>(o)).ToList(),
                    data.Tags.Where(o => !o.TagKey.StartsWith("$")),
                    retVal.Key,
                    context);

            // Participations - We don't touch as Act > Participation
            //if(data.Participations != null)
            //{
            //    foreach (var itm in data.Participations)
            //    {
            //        itm.PlayerEntityKey = retVal.Key;
            //        itm.Act?.EnsureExists(context);
            //        itm.SourceEntityKey = itm.Act?.Key ?? itm.SourceEntityKey;
            //    } 
            //    var existing = context.Table<DbActParticipation>().Where(o => o.EntityUuid == entityUuid).ToList().Select(o => m_mapper.MapDomainInstance<DbActParticipation, ActParticipation>(o)).ToList();
            //    base.UpdateAssociatedItems<ActParticipation, Act>(
            //        existing,
            //        data.Participations,
            //        retVal.Key,
            //        context, 
            //        true);
            //}


            return retVal;
        }

        private TableQuery<object> DbActPersistence()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Obsoleted status key
        /// </summary>
        protected override Entity ObsoleteInternal(SQLiteDataContext context, Entity data)
        {
            data.StatusConceptKey = StatusKeys.Obsolete;
            return base.ObsoleteInternal(context, data);
        }

        /// <summary>
        /// Insert the object
        /// </summary>
        protected override Entity InsertInternal(SQLiteDataContext context, Entity data)
        {
            switch (data.ClassConceptKey.ToString().ToUpper())
            {
                case EntityClassKeyStrings.Device:
                    return new DeviceEntityPersistenceService().Insert(context, data as DeviceEntity);
                case EntityClassKeyStrings.NonLivingSubject:
                    return new ApplicationEntityPersistenceService().Insert(context, data as ApplicationEntity);
                case EntityClassKeyStrings.Person:
                    if(data is UserEntity)
                        return new UserEntityPersistenceService().Insert(context, data as UserEntity);
                    else
                        return new PersonPersistenceService().Insert(context, data as Person);
                case EntityClassKeyStrings.Patient:
                    return new PatientPersistenceService().Insert(context, data as Patient);
                case EntityClassKeyStrings.Provider:
                    return new ProviderPersistenceService().Insert(context, data as Provider);
                case EntityClassKeyStrings.Place:
                case EntityClassKeyStrings.CityOrTown:
                case EntityClassKeyStrings.Country:
                case EntityClassKeyStrings.CountyOrParish:
                case EntityClassKeyStrings.State:
                case EntityClassKeyStrings.ServiceDeliveryLocation:
                case EntityClassKeyStrings.PrecinctOrBurrogh:
                    return new PlacePersistenceService().Insert(context, data as Place);
                case EntityClassKeyStrings.Organization:
                    return new OrganizationPersistenceService().Insert(context, data as Organization);
                case EntityClassKeyStrings.Material:
                    return new MaterialPersistenceService().Insert(context, data as Material);
                case EntityClassKeyStrings.ManufacturedMaterial:
                    return new ManufacturedMaterialPersistenceService().Insert(context, data as ManufacturedMaterial);
                default:
                    return this.InsertCoreProperties(context, data);

            }
        }

        /// <summary>
        /// Insert the object
        /// </summary>
        protected override Entity UpdateInternal(SQLiteDataContext context, Entity data)
        {
            switch (data.ClassConceptKey.ToString().ToUpper())
            {
                case EntityClassKeyStrings.Device:
                    return new DeviceEntityPersistenceService().Update(context, data as DeviceEntity);
                case EntityClassKeyStrings.NonLivingSubject:
                    return new ApplicationEntityPersistenceService().Update(context, data as ApplicationEntity);
                case EntityClassKeyStrings.Person:
                    if (data is UserEntity)
                        return new UserEntityPersistenceService().Update(context, data as UserEntity);
                    else
                        return new PersonPersistenceService().Update(context, data as Person);
                case EntityClassKeyStrings.Patient:
                    return new PatientPersistenceService().Update(context, data as Patient);
                case EntityClassKeyStrings.Provider:
                    return new ProviderPersistenceService().Update(context, data as Provider);
                case EntityClassKeyStrings.Place:
                case EntityClassKeyStrings.CityOrTown:
                case EntityClassKeyStrings.Country:
                case EntityClassKeyStrings.CountyOrParish:
                case EntityClassKeyStrings.State:
                case EntityClassKeyStrings.ServiceDeliveryLocation:
                case EntityClassKeyStrings.PrecinctOrBurrogh:
                    return new PlacePersistenceService().Update(context, data as Place);
                case EntityClassKeyStrings.Organization:
                    return new OrganizationPersistenceService().Update(context, data as Organization);
                case EntityClassKeyStrings.Material:
                    return new MaterialPersistenceService().Update(context, data as Material);
                case EntityClassKeyStrings.ManufacturedMaterial:
                    return new ManufacturedMaterialPersistenceService().Update(context, data as ManufacturedMaterial);
                default:
                    return this.UpdateCoreProperties(context, data);

            }
        }

    }
}
