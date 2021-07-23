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
using Newtonsoft.Json;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Security;
using SanteDB.Messaging.HDSI.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Represents a persistence service which uses the HDSI only in online mode
    /// </summary>
    public class RemoteRepositoryService : IDaemonService
    {

        /// <summary>
        /// Template keys
        /// </summary>
        private static ConcurrentDictionary<String, Guid> s_templateKeys = new ConcurrentDictionary<string, Guid>();

        /// <summary>
        /// Master entity
        /// </summary>
        internal interface IMasterEntity { }

        /// <summary>
        /// Represents a relationship shim for MDM
        /// </summary>
        [XmlType(Namespace = "http://santedb.org/model")]
        public class EntityRelationshipMaster : EntityRelationship
        {


            /// <summary>
            /// Gets the original relationship
            /// </summary>
            [XmlElement("originalHolder"), JsonProperty("originalHolder")]
            public Guid? OriginalHolderKey { get; set; }

            /// <summary>
            /// Gets the original relationship
            /// </summary>
            [XmlElement("originalTarget"), JsonProperty("originalTarget")]
            public Guid? OriginalTargetKey { get; set; }

        }

        /// <summary>
        /// Stub class for receiving MDM Entities
        /// </summary>
        [XmlType(Namespace = "http://santedb.org/model")]
        public class EntityMaster<T> : Entity, IMasterEntity
            where T : IdentifiedData, new()
        { }

        /// <summary>
        /// Stub class for receiving MDM Acts
        /// </summary>
        [XmlType(Namespace = "http://santedb.org/model")]
        public class ActMaster<T> : Act, IMasterEntity
            where T : IdentifiedData, new()
        { }



        /// <summary>
        /// Get all types from core classes of entity and act and create shims in the model serialization binder
        /// </summary>
        static RemoteRepositoryService()
        {
            foreach (var t in typeof(Entity).GetTypeInfo().Assembly.ExportedTypes.Where(o => typeof(Entity).GetTypeInfo().IsAssignableFrom(o.GetTypeInfo())))
                ModelSerializationBinder.RegisterModelType(typeof(EntityMaster<>).MakeGenericType(t));
            foreach (var t in typeof(Act).GetTypeInfo().Assembly.ExportedTypes.Where(o => typeof(Act).GetTypeInfo().IsAssignableFrom(o.GetTypeInfo())))
                ModelSerializationBinder.RegisterModelType(typeof(ActMaster<>).MakeGenericType(t));
            ModelSerializationBinder.RegisterModelType(typeof(EntityRelationshipMaster));

        }



        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Remote Data Repository Service";

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteRepositoryService));

        /// <summary>
        /// Return true if running
        /// </summary>
        public bool IsRunning => false;

        public event EventHandler Starting;
        public event EventHandler Started;
        public event EventHandler Stopping;
        public event EventHandler Stopped;

        /// <summary>
        /// Start the service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            var appSection = ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>();

            // Now iterate through the map file and ensure we have all the mappings, if a class does not exist create it
            try
            {

                foreach (var itm in typeof(IdentifiedData).GetTypeInfo().Assembly.ExportedTypes.Where(o => typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(o.GetTypeInfo()) && !o.GetTypeInfo().IsAbstract))
                {

                    var rootElement = itm.GetTypeInfo().GetCustomAttribute<XmlRootAttribute>();
                    if (rootElement == null) continue;
                    // Is there a persistence service?
                    var idpType = typeof(IRepositoryService<>);
                    idpType = idpType.MakeGenericType(itm);

                    this.m_tracer.TraceVerbose("Creating persister {0}", itm);

                    // Is the model class a Versioned entity?
                    var pclass = typeof(RemoteRepositoryService<>);
                    pclass = pclass.MakeGenericType(itm);

                    if (ApplicationServiceContext.Current.GetService(idpType) == null)
                        ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(pclass);
                }

                // Get client for device user
                try
                {
                    using (var client = GetClient())
                    {
                        var retVal = client.Query<TemplateDefinition>(o => o.ObsoletionTime == null);
                        retVal.Item.OfType<TemplateDefinition>().ToList().ForEach(o => s_templateKeys.TryAdd(o.Mnemonic, o.Key.Value));
                    }
                }
                catch(Exception e)
                {
                    this.m_tracer.TraceWarning("Cannot map local template keys");
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error initializing local persistence: {0}", e);
                throw e;
            }

            this.Started?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Get the client
        /// </summary>
        private static HdsiServiceClient GetClient()
        {
            var retVal = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
            
            var appConfig = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SecurityConfigurationSection>();
            var rmtPrincipal = ApplicationServiceContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret);
            retVal.Client.Credentials = retVal.Client.Description.Binding.Security.CredentialProvider.GetCredentials(rmtPrincipal);
            return retVal;
        }

        /// <summary>
        /// Get the specified template by mnemonic
        /// </summary>
        internal static Guid? GetTemplate(string mnemonic)
        {
            if(!s_templateKeys.TryGetValue(mnemonic, out Guid retVal))
            {
                // Get client for device user
                using (var client = GetClient())
                {
                    var itm = client.Query<TemplateDefinition>(o => o.Mnemonic == mnemonic);
                    itm.Item.OfType<TemplateDefinition>().ToList().ForEach(o => s_templateKeys.TryAdd(o.Mnemonic, o.Key.Value));
                }
            }
            return retVal;
        }
    }


    /// <summary>
    /// Generic versioned persister service for any non-customized persister
    /// </summary>
    internal class RemoteRepositoryService<TModel> : IRepositoryService<TModel>, IPersistableQueryRepositoryService<TModel>
        where TModel : IdentifiedData, new()
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => $"Remote repository for {typeof(TModel).FullName}";

        // Used to reduce requests to the server which the server had previously rejected
        //private HashSet<Guid> m_missEntity = new HashSet<Guid>();

        /// <summary>
        /// Get the client
        /// </summary>
        public HdsiServiceClient GetClient()
        {
            var retVal = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
            retVal.Client.Credentials = retVal.Client.Description.Binding.Security?.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);
            return retVal;
        }

        /// <summary>
        /// Get the specified object
        /// </summary>
        public TModel Get(Guid key)
        {
            return this.Get(key, Guid.Empty);
        }

        /// <summary>
        /// Gets the specified item
        /// </summary>
        public TModel Get(Guid key, Guid versionKey)
        {
            using (var client = this.GetClient())
                try
                {
                    var existing = ApplicationContext.Current.GetService<IDataCachingService>()?.GetCacheItem(key) as IdentifiedData;

                    if (existing is TModel)
                    {
                        if (existing != null && existing is IdentifiedData idata) // For entities and acts we want to ping the server 
                        {
                            client.Client.Requesting += (o, e) => e.AdditionalHeaders.Add("If-None-Match", idata.Tag);
                        }
                        existing = client.Get<TModel>(key, versionKey == Guid.Empty ? (Guid?)null : versionKey) as TModel ?? existing;
                    }
                    else
                    {
                        existing = client.Get<TModel>(key, versionKey == Guid.Empty ? (Guid?)null : versionKey) as TModel;
                    }
                    // Add if existing key is same newest version
                    if (versionKey == Guid.Empty)
                        ApplicationContext.Current.GetService<IDataCachingService>()?.Add(existing as IdentifiedData);
                    return (TModel)existing;
                }
                catch (WebException)
                {
                    //lock (this.m_missEntity)
                    //    this.m_missEntity.Add(key);
                    // Web exceptions should not bubble up
                    return default(TModel);
                }
        }

        /// <summary>
        /// Harmonize the template identifiers
        /// </summary>
        private void HarmonizeTemplateId(IHasTemplate template)
        {
            if(template.Template != null && 
                !template.TemplateKey.HasValue)
            {
                // Lookup 
                template.TemplateKey = RemoteRepositoryService.GetTemplate(template.Template.Mnemonic);
            }
        }

        /// <summary>
        /// Inserts the specified typed data
        /// </summary>
        public TModel Insert(TModel data)
        {

            if (data is IHasTemplate template)
                this.HarmonizeTemplateId(template);
            else if (data is Bundle bundle)
                bundle.Item.OfType<IHasTemplate>().ToList().ForEach(o => this.HarmonizeTemplateId(o));

            using (var client = this.GetClient())
            {
                var retVal = client.Create(data);
                ApplicationContext.Current.GetService<IDataCachingService>()?.Add(retVal);
                return retVal;
            }
        }

        /// <summary>
        /// Obsoletes the specified data
        /// </summary>
        public TModel Obsolete(Guid key)
        {
            using (var client = this.GetClient())
            {
                var retVal = client.Obsolete(new TModel() { Key = key });
                ApplicationContext.Current.GetService<IDataCachingService>()?.Remove(key);
                return retVal;
            }
        }

        /// <summary>
        /// Query the specified data
        /// </summary>
        public IEnumerable<TModel> Find(Expression<Func<TModel, bool>> query)
        {
            int t;
            return this.Find(query, 0, null, out t);
        }

        /// <summary>
        /// Query the specifie data
        /// </summary>
        public IEnumerable<TModel> Find(Expression<Func<TModel, bool>> query, int offset, int? count, out int totalResults, params ModelSort<TModel>[] orderBy)
        {
            return this.Find(query, offset, count, out totalResults, Guid.Empty, orderBy);

        }

        /// <summary>
        /// Update the specified data
        /// </summary>
        public TModel Save(TModel data)
        {
            if (data is IHasTemplate template)
                this.HarmonizeTemplateId(template);
            else if (data is Bundle bundle)
                bundle.Item.OfType<IHasTemplate>().ToList().ForEach(o => this.HarmonizeTemplateId(o));

            using (var client = this.GetClient())
            {
                var retVal = client.Update(data);
                ApplicationContext.Current.GetService<IDataCachingService>()?.Add(retVal);
                return retVal;
            }
        }

        /// <summary>
        /// Find the specified objects
        /// </summary>
        public IEnumerable<TModel> Find(Expression<Func<TModel, bool>> query, int offset, int? count, out int totalResults, Guid queryId, params ModelSort<TModel>[] orderBy)
        {
            using (var client = this.GetClient())
                try
                {
                    var data = client.Query(query, offset, count, false, queryId: queryId, orderBy: orderBy);
                    (data as Bundle)?.Reconstitute();
                    offset = (data as Bundle)?.Offset ?? offset;
                    count = (data as Bundle)?.Count ?? count;
                    totalResults = (data as Bundle)?.TotalResults ?? 1;

                    // Reconstitute the bundle
                    (data as Bundle)?.Reconstitute();
                    //data.Item.RemoveAll(o => data.ExpansionKeys.Contains(o.Key.Value));
                    //data.ExpansionKeys.Clear();
                    // TODO: Only process Focal objects 
                    data.Item.AsParallel().ForAll(o =>
                    {
                        ApplicationContext.Current.GetService<IDataCachingService>()?.Add(o as IdentifiedData);
                    });

                    return (data as Bundle)?.Item.OfType<TModel>() ?? new List<TModel>() { data as TModel };
                }
                catch (WebException)
                {
                    totalResults = 0;
                    return new List<TModel>();
                }
        }
    }

}