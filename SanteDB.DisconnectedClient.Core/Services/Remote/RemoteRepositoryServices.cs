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
 * Date: 2018-11-23
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.Messaging.HDSI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Core.Services.Remote
{
    /// <summary>
    /// Represents a persistence service which uses the HDSI only in online mode
    /// </summary>
    public class RemoteRepositoryService : IDaemonService
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteRepositoryService));

        // Constructor
        public RemoteRepositoryService()
        {

        }

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

                    if (ApplicationContext.Current.GetService(idpType) == null)
                        ApplicationContext.Current.AddServiceProvider(pclass);
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
    }

    /// <summary>
    /// Generic versioned persister service for any non-customized persister
    /// </summary>
    internal class RemoteRepositoryService<TModel> : IRepositoryService<TModel>
        where TModel : IdentifiedData, new()
    {
        // Service client
        private HdsiServiceClient m_client = null;

        // Used to reduce requests to the server which the server had previously rejected
        private HashSet<Guid> m_missEntity = new HashSet<Guid>();

        public RemoteRepositoryService()
        {
            this.m_client = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
            this.m_client.Client.Requesting += (o, e) =>
            {
                e.Query.Add("_expand", new List<String>() {
                        "typeConcept",
                        "address.use",
                        "name.use"
                });
            };

        }

        private IPrincipal m_cachedCredential = null;

        /// <summary>
        /// Gets current credentials
        /// </summary>
        private Credentials GetCredentials()
        {
            var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();
            AuthenticationContext.Current = new AuthenticationContext(this.m_cachedCredential ?? AuthenticationContext.Current.Principal);
            return this.m_client.Client.Description.Binding.Security.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);
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
            this.GetCredentials();

            try
            {
                var existing = ApplicationContext.Current.GetService<IDataCachingService>()?.GetCacheItem(key) as IdentifiedData;
                if (existing != null)
                { // check the cache to see if it is stale
                    string etag = null;
                    this.m_client.Client.Head($"{typeof(TModel).GetTypeInfo().GetCustomAttribute<XmlRootAttribute>().ElementName}/{key}").TryGetValue("ETag", out etag);
                    if (versionKey != Guid.Empty) // not versioned so who cares!?
                        ;
                    else if (etag != (existing as IdentifiedData).Tag) // Versions don't match the latest
                        ApplicationContext.Current.GetService<IDataCachingService>()?.Remove(existing.Key.Value);
                }
                if (!this.m_missEntity.Contains(key) && (existing == null ||
                    (versionKey != Guid.Empty && (existing as IVersionedEntity)?.VersionKey != versionKey)))
                {
                    existing = this.m_client.Get<TModel>(key, versionKey == Guid.Empty ? (Guid?)null : versionKey) as TModel;

                    // Add if existing key is same newest version
                    if (versionKey == Guid.Empty)
                        ApplicationContext.Current.GetService<IDataCachingService>()?.Add(existing as IdentifiedData);
                }
                return (TModel)existing;
            }
            catch (WebException)
            {
                lock (this.m_missEntity)
                    this.m_missEntity.Add(key);
                // Web exceptions should not bubble up
                return default(TModel);
            }
        }

        /// <summary>
        /// Inserts the specified typed data
        /// </summary>
        public TModel Insert(TModel data)
        {
            this.GetCredentials();

            var retVal = this.m_client.Create(data);
            return retVal;
        }

        /// <summary>
        /// Obsoletes the specified data
        /// </summary>
        public TModel Obsolete(Guid key)
        {
            this.GetCredentials();

            var retVal = this.m_client.Obsolete(new TModel() { Key = key });
            return retVal;
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
        public IEnumerable<TModel> Find(Expression<Func<TModel, bool>> query, int offset, int? count, out int totalResults)
        {
            this.GetCredentials();

            try
            {
                var data = this.m_client.Query(query, offset, count, false);
                (data as Bundle)?.Reconstitute();
                offset = (data as Bundle)?.Offset ?? offset;
                count = (data as Bundle)?.Count ?? count;
                totalResults = (data as Bundle)?.TotalResults ?? 1;

                // Reconstitute the bundle
                (data as Bundle)?.Reconstitute();
                data.Item.RemoveAll(o => data.ExpansionKeys.Contains(o.Key.Value));
                data.ExpansionKeys.Clear();
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

        /// <summary>
        /// Update the specified data
        /// </summary>
        public TModel Save(TModel data)
        {
            this.GetCredentials();

            var retVal = this.m_client.Update(data);
            return retVal;
        }

    }

}