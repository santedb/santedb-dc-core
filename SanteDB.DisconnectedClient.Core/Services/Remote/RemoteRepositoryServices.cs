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
using Newtonsoft.Json;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
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
using SanteDB.Messaging.HDSI.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Remote repository service (deprecated - kept for backwards compatibility with configurations)
    /// </summary>
    [Obsolete("Use SanteDB.DisconnectedClient.Services.RemoteRepositoryFactory", true)]
    public class RemoteRepositoryService : RemoteRepositoryFactory
    {
        public RemoteRepositoryService(IConfigurationManager configurationManager, IServiceManager serviceManager, ILocalizationService localizationService) : base(configurationManager, serviceManager, localizationService)
        {
        }
    }

    /// <summary>
    /// Generic versioned persister service for any non-customized persister
    /// </summary>
    internal class RemoteRepositoryService<TModel> : IRepositoryService<TModel>, IPersistableQueryRepositoryService<TModel>, IRepositoryService
        where TModel : IdentifiedData, new()
    {
        // Template keys already fetched from the server
        private ConcurrentDictionary<String, Guid> s_templateKeys = new ConcurrentDictionary<string, Guid>();

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
            if (template.Template != null &&
                !template.TemplateKey.HasValue)
            {
                if (!s_templateKeys.TryGetValue(template.Template.Mnemonic, out Guid retVal))
                {
                    using (var client = GetClient())
                    {
                        var itm = client.Query<TemplateDefinition>(o => o.Mnemonic == template.Template.Mnemonic);
                        itm.Item.OfType<TemplateDefinition>().ToList().ForEach(o => s_templateKeys.TryAdd(o.Mnemonic, o.Key.Value));
                    }
                }
                template.TemplateKey = retVal;
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
                
        }

        /// <summary>
        /// Get by key
        /// </summary>
        IdentifiedData IRepositoryService.Get(Guid key) => this.Get(key);

        /// <summary>
        /// Find
        /// </summary>
        public IEnumerable<IdentifiedData> Find(Expression query) => this.Find(query as Expression<Func<TModel, bool>>);

        /// <summary>
        /// Find with restrictions
        /// </summary>
        public IEnumerable<IdentifiedData> Find(Expression query, int offset, int? count, out int totalResults) => this.Find(query as Expression<Func<TModel, bool>>, offset, count, out totalResults);

        /// <summary>
        /// Insert data
        /// </summary>
        public IdentifiedData Insert(object data) => this.Insert(data as TModel);

        /// <summary>
        /// Save
        /// </summary>
        public IdentifiedData Save(object data) => this.Save(data as TModel);

        /// <summary>
        /// Obsolete data
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        IdentifiedData IRepositoryService.Obsolete(Guid key) => this.Obsolete(key);
    }
}