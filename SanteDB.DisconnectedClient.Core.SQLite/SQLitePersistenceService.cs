/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Attributes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Persistence;
using SanteDB.DisconnectedClient.SQLite.Warehouse;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.SQLite
{


    /// <summary>
    /// Represents a dummy service which just adds the persistence services to the context
    /// </summary>
    public class SQLitePersistenceService
    {

        // Cache
        private static Dictionary<Type, ISQLitePersistenceService> s_persistenceCache = new Dictionary<Type, ISQLitePersistenceService>();

        private DcDataConfigurationSection m_configuration = ApplicationContext.Current.GetService<IConfigurationManager>().GetSection<DcDataConfigurationSection>();

        /// <summary>
        /// Get the specified persister type
        /// </summary>
        public static ISQLitePersistenceService GetPersister(Type tDomain)
        {
            ISQLitePersistenceService retVal = null;
            if (!s_persistenceCache.TryGetValue(tDomain, out retVal))
            {
                var idpType = typeof(IDataPersistenceService<>).MakeGenericType(tDomain);
                retVal = ApplicationContext.Current.GetService(idpType) as ISQLitePersistenceService;
                if (retVal != null)
                    lock (s_persistenceCache)
                        if (!s_persistenceCache.ContainsKey(tDomain))
                            s_persistenceCache.Add(tDomain, retVal);
            }
            return retVal;
        }

        /// <summary>
        /// Generic versioned persister service for any non-customized persister
        /// </summary>
        internal class GenericVersionedPersistenceService<TModel, TDomain> : VersionedDataPersistenceService<TModel, TDomain>
            where TDomain : DbVersionedData, new()
            where TModel : VersionedEntityData<TModel>, new()
        {

            /// <summary>
            /// Ensure exists
            /// </summary>
            protected override TModel InsertInternal(SQLiteDataContext context, TModel data)
            {
                foreach (var rp in typeof(TModel).GetRuntimeProperties().Where(o => typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(o.PropertyType.GetTypeInfo())))
                {
                    if (rp.GetCustomAttribute<DataIgnoreAttribute>() != null) continue;

                    var instance = rp.GetValue(data);
                    if (instance != null)
                    {
                        instance = ModelExtensions.TryGetExisting(instance as IIdentifiedEntity, context);
                        if (instance != null) rp.SetValue(data, instance);
                        ModelExtensions.UpdateParentKeys(data, rp);
                    }
                }
                return base.InsertInternal(context, data);
            }

            /// <summary>
            /// Update the specified object
            /// </summary>
            protected override TModel UpdateInternal(SQLiteDataContext context, TModel data)
            {
                foreach (var rp in typeof(TModel).GetRuntimeProperties().Where(o => typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(o.PropertyType.GetTypeInfo())))
                {
                    if (rp.GetCustomAttribute<DataIgnoreAttribute>() != null) continue;

                    var instance = rp.GetValue(data);
                    if (instance != null)
                    {
                        instance = ModelExtensions.TryGetExisting(instance as IIdentifiedEntity, context);
                        if (instance != null) rp.SetValue(data, instance);
                        ModelExtensions.UpdateParentKeys(data, rp);
                    }

                }
                return base.UpdateInternal(context, data);
            }
        }

        /// <summary>
        /// Generic versioned persister service for any non-customized persister
        /// </summary>
        internal class GenericBasePersistenceService<TModel, TDomain> : BaseDataPersistenceService<TModel, TDomain>
            where TDomain : DbBaseData, new()
            where TModel : BaseEntityData, new()
        {

            /// <summary>
            /// Ensure exists
            /// </summary>
            protected override TModel InsertInternal(SQLiteDataContext context, TModel data)
            {
                if (data.IsEmpty()) return data;

                foreach (var rp in typeof(TModel).GetRuntimeProperties().Where(o => typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(o.PropertyType.GetTypeInfo())))
                {
                    if (rp.GetCustomAttribute<DataIgnoreAttribute>() != null) continue;

                    var instance = rp.GetValue(data);
                    if (instance != null)
                    {
                        instance = ModelExtensions.EnsureExists(instance as IIdentifiedEntity, context);
                        if (instance != null) rp.SetValue(data, instance);
                        ModelExtensions.UpdateParentKeys(data, rp);
                    }
                }
                return base.InsertInternal(context, data);
            }

            /// <summary>
            /// Update the specified object
            /// </summary>
            protected override TModel UpdateInternal(SQLiteDataContext context, TModel data)
            {
                if (data.IsEmpty()) return data;

                foreach (var rp in typeof(TModel).GetRuntimeProperties().Where(o => typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(o.PropertyType.GetTypeInfo())))
                {
                    if (rp.GetCustomAttribute<DataIgnoreAttribute>() != null) continue;

                    var instance = rp.GetValue(data);
                    if (instance != null)
                    {
                        ModelExtensions.EnsureExists(instance as IIdentifiedEntity, context);
                        if (instance != null) rp.SetValue(data, instance);
                        ModelExtensions.UpdateParentKeys(data, rp);
                    }

                }
                return base.UpdateInternal(context, data);
            }
        }

        /// <summary>
        /// Generic versioned persister service for any non-customized persister
        /// </summary>
        internal class GenericIdentityPersistenceService<TModel, TDomain> : IdentifiedPersistenceService<TModel, TDomain>
            where TModel : IdentifiedData, new()
            where TDomain : DbIdentified, new()
        {

            // Properties
            private List<PropertyInfo> m_properties = new List<PropertyInfo>();

            /// <summary>
            /// Properties
            /// </summary>
            public GenericIdentityPersistenceService()
            {
                this.m_properties = typeof(TModel).GetRuntimeProperties().Where(o => typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(o.PropertyType.GetTypeInfo())).Where(o => o.GetCustomAttribute<DataIgnoreAttribute>() == null).ToList();
            }

            /// <summary>
            /// Ensure exists
            /// </summary>
            protected override TModel InsertInternal(SQLiteDataContext context, TModel data)
            {
                if (data.IsEmpty()) return data;

                foreach (var rp in this.m_properties)
                {

                    var instance = rp.GetValue(data);
                    if (instance != null)
                    {
                        instance = ModelExtensions.TryGetExisting(instance as IIdentifiedEntity, context);
                        if (instance != null) rp.SetValue(data, instance);

                        ModelExtensions.UpdateParentKeys(data, rp);
                    }

                }
                return base.InsertInternal(context, data);
            }

            /// <summary>
            /// Update the specified object
            /// </summary>
            protected override TModel UpdateInternal(SQLiteDataContext context, TModel data)
            {
                if (data.IsEmpty()) return data;

                foreach (var rp in this.m_properties)
                {

                    var instance = rp.GetValue(data);
                    if (instance != null && rp.Name != "SourceEntity") // HACK: Prevent infinite loops on associtive entities
                    {
                        instance = ModelExtensions.TryGetExisting(instance as IIdentifiedEntity, context);
                        if (instance != null) rp.SetValue(data, instance);
                        ModelExtensions.UpdateParentKeys(data, rp);
                    }

                }
                return base.UpdateInternal(context, data);
            }
        }

        /// <summary>
        /// Generic association persistence service
        /// </summary>
        internal class GenericIdentityAssociationPersistenceService<TModel, TDomain> :
            GenericIdentityPersistenceService<TModel, TDomain>, ISQLiteAssociativePersistenceService
            where TModel : IdentifiedData, ISimpleAssociation, new()
            where TDomain : DbIdentified, new()
        {
            /// <summary>
            /// Get all the matching TModel object from source
            /// </summary>
            public IEnumerable GetFromSource(SQLiteDataContext context, Guid sourceId, decimal? versionSequenceId)
            {
                int tr = 0;
                return this.Query(context, o => o.SourceEntityKey == sourceId, Guid.Empty, 0, 100, out tr, false, null);
            }
        }

        // Mapper
        protected static ModelMapper m_mapper;

        // Static CTOR
        public static ModelMapper Mapper
        {
            get
            {
                if (m_mapper == null)
                {
                    var tracer = Tracer.GetTracer(typeof(SQLitePersistenceService));
                    try
                    {
                        m_mapper = new ModelMapper(typeof(SQLitePersistenceService).GetTypeInfo().Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.SQLite.Map.ModelMap.xml"));
                    }
                    catch (ModelMapValidationException ex)
                    {
                        tracer.TraceError("Error validating model map: {0}", ex);
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        tracer.TraceError("Error initializing persistence: {0}", ex);
                        throw ex;
                    }
                }
                return m_mapper;
            }
        }

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLitePersistenceService));

        /// <summary>
        /// Construct persistence service for SQLite and register subordinate services
        /// </summary>
        public SQLitePersistenceService()
        {
            this.m_tracer.TraceInfo("Starting local persistence services...");
            // Iterate the persistence services
            foreach (var t in typeof(SQLitePersistenceService).GetTypeInfo().Assembly.ExportedTypes.Where(o => o.Namespace == "SanteDB.DisconnectedClient.SQLite.Persistence" && !o.GetTypeInfo().IsAbstract && !o.GetTypeInfo().IsGenericTypeDefinition))
            {
                try
                {
                    this.m_tracer.TraceVerbose("Loading {0}...", t.AssemblyQualifiedName);
                    ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(t);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error adding service {0} : {1}", t.AssemblyQualifiedName, e);
                }
            }

            // Now iterate through the map file and ensure we have all the mappings, if a class does not exist create it
            try
            {
                this.m_tracer.TraceVerbose("Creating secondary model maps...");

                var map = ModelMap.Load(typeof(SQLitePersistenceService).GetTypeInfo().Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.SQLite.Map.ModelMap.xml"));
                foreach (var itm in map.Class)
                {
                    // Is there a persistence service?
                    var idpType = typeof(IDataPersistenceService<>);
                    Type modelClassType = Type.GetType(itm.ModelClass),
                        domainClassType = Type.GetType(itm.DomainClass);
                    idpType = idpType.MakeGenericType(modelClassType);


                    // Already created
                    if (ApplicationContext.Current.GetService(idpType) != null)
                        continue;

                    this.m_tracer.TraceVerbose("Creating map {0} > {1}", modelClassType, domainClassType);

                    // Is the model class a Versioned entity?
                    if (modelClassType.GetRuntimeProperty("VersionKey") != null &&
                        typeof(DbVersionedData).GetTypeInfo().IsAssignableFrom(domainClassType.GetTypeInfo()))
                    {
                        // Construct a type
                        var pclass = typeof(GenericVersionedPersistenceService<,>);
                        pclass = pclass.MakeGenericType(modelClassType, domainClassType);
                        ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(pclass);
                    }
                    else if (modelClassType.GetRuntimeProperty("CreatedByKey") != null &&
                        typeof(DbBaseData).GetTypeInfo().IsAssignableFrom(domainClassType.GetTypeInfo()))
                    {
                        // Construct a type
                        var pclass = typeof(GenericBasePersistenceService<,>);
                        pclass = pclass.MakeGenericType(modelClassType, domainClassType);
                        ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(pclass);
                    }
                    else
                    {
                        // Construct a type
                        Type pclass = null;
                        if (modelClassType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IVersionedAssociation)))
                            pclass = typeof(GenericIdentityAssociationPersistenceService<,>);
                        else if (modelClassType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(ISimpleAssociation)))
                            pclass = typeof(GenericIdentityAssociationPersistenceService<,>);
                        else
                            pclass = typeof(GenericIdentityPersistenceService<,>);
                        pclass = pclass.MakeGenericType(modelClassType, domainClassType);
                        ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(pclass);
                    }

                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error initializing local persistence: {0}", e);
                throw;
            }

            ApplicationServiceContext.Current.Started += (o, e) =>
            {
                // TODO: Subscriptions on SQLite
                //ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(typeof(AdoSubscriptionExector));

                // Bind BI stuff
                ApplicationServiceContext.Current.GetService<IBiMetadataRepository>()?.Insert(new SanteDB.BI.Model.BiDataSourceDefinition()
                {
                    MetaData = new BiMetadata()
                    {
                        Version = typeof(SQLitePersistenceService).GetTypeInfo().Assembly.GetName().Version.ToString(),
                        Status = BiDefinitionStatus.Active,
                    },
                    Id = "org.santedb.bi.dataSource.main",
                    Name = "main",
                    ConnectionString = m_configuration.MainDataSourceConnectionStringName,
                    ProviderType = typeof(SQLiteBiDataSource),
                    IsSystemObject = true
                });

            };
        }
    }
}

