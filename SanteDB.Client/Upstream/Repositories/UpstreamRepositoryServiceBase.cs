/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using SanteDB.Client.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// A generic implementation that calls the upstream for fetching data 
    /// </summary>
    public abstract class UpstreamRepositoryServiceBase<TModel, TWireFormat, TCollection> : UpstreamServiceBase,
        IRepositoryServiceEx<TModel>,
        IRepositoryService
        where TModel : IdentifiedData, new()
        where TWireFormat : class, IIdentifiedResource, new()
        where TCollection : IResourceCollection
    {
        private readonly ServiceEndpointType m_endpoint;
        private readonly IDataCachingService m_cacheService;
        private readonly ILocalizationService m_localeService;
        private readonly IAdhocCacheService m_adhocCache;

        /// <summary>
        /// Gets the localization service
        /// </summary>
        protected ILocalizationService LocalizationService => this.m_localeService;

        /// <summary>
        /// Gets the data caching service
        /// </summary>
        protected IDataCachingService DataCachingService => this.m_cacheService;

        /// <summary>
        /// DI constructor
        /// </summary>
        protected UpstreamRepositoryServiceBase(
            ServiceEndpointType serviceEndpoint,
            ILocalizationService localizationService,
            IDataCachingService cacheService,
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService,
            IAdhocCacheService adhocCacheService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_endpoint = serviceEndpoint;
            this.m_cacheService = cacheService;
            this.m_localeService = localizationService;
            this.m_adhocCache = adhocCacheService;
        }

        /// <summary>
        /// Map the <paramref name="wireObject"/> received from the remote server into a <typeparamref name="TModel"/>
        /// </summary>
        protected virtual TModel MapFromWire(TWireFormat wireObject) => wireObject as TModel;

        /// <summary>
        /// Map the <paramref name="modelObject"/> to a wire appropriate format
        /// </summary>
        protected virtual TWireFormat MapToWire(TModel modelObject) => modelObject as TWireFormat;

        /// <inheritdoc/>
        public string ServiceName => $"Upstream Repository for {typeof(TModel)}";

        /// <summary>
        /// Get resource name on the API
        /// </summary>
        protected virtual string GetResourceName() => typeof(TModel).GetResourceName();

        /// <inheritdoc/>
        public virtual TModel Delete(Guid key)
        {

            try
            {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    var retVal = this.MapFromWire(client.Delete<TWireFormat>($"{this.GetResourceName()}/{key}"));
                    this.m_cacheService?.Remove(key);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_GEN_ERR), e);
            }

        }

        /// <inheritdoc/>
        public virtual IQueryResultSet<TModel> Find(Expression<Func<TModel, bool>> query)
        {
            // Determine if we have a result already?
            return new UpstreamQueryResultSet<TModel, TWireFormat, TCollection>(this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal), query, this.MapFromWire);
        }

        /// <inheritdoc/>
        [Obsolete("Use Find(query)", true)]
        public virtual IEnumerable<TModel> Find(Expression<Func<TModel, bool>> query, int offset, int? count, out int totalResults, params ModelSort<TModel>[] orderBy)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual TModel Get(Guid key) => this.Get(key, Guid.Empty);

        /// <inheritdoc/>
        public virtual TModel Get(Guid key, Guid versionKey)
        {

            try
            {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    var existing = this.m_cacheService?.GetCacheItem(key);

                    if (existing is TModel tm && versionKey == Guid.Empty) // The cache item matches the type
                    {
                        var lastCheckKey = existing.Tag;
                        // Only do a head if the ad-hoc cache for excessive HEAD checks is null
                        if (!this.m_adhocCache.TryGet<DateTime>(lastCheckKey, out var lastTimeChecked))
                        {
                            client.Requesting += (o, e) => e.AdditionalHeaders.Add("If-None-Match", $"{tm.Tag}");
                            existing = this.MapFromWire(client.Get<TWireFormat>($"{this.GetResourceName()}/{key}")) ?? existing;
                            this.m_adhocCache.Add(lastCheckKey, DateTime.Now, new TimeSpan(0, 2, 00));
                        }
                    }
                    else if (versionKey == Guid.Empty)
                    {
                        existing = this.MapFromWire(client.Get<TWireFormat>($"{this.GetResourceName()}/{key}"));
                        this.m_cacheService?.Add(existing as IdentifiedData);
                    }
                    else
                    {
                        existing = this.MapFromWire(client.Get<TWireFormat>($"{this.GetResourceName()}/{key}/_history/{versionKey}"));
                    }

                    return (TModel)existing;
                }
            }
            catch (WebException) // Web based exception = return nothing
            {
                return default(TModel);
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }

        }


        /// <inheritdoc/>
        public virtual TModel Insert(TModel data)
        {

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data is IHasTemplate iht)
            {
                this.UpstreamIntegrationService.HarmonizeTemplateId(iht);
            }
            else if (data is IResourceCollection irc)
            {
                irc.Item.OfType<IHasTemplate>().ToList().ForEach(r => this.UpstreamIntegrationService.HarmonizeTemplateId(r));
            }


            try
            {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    var retVal = this.MapFromWire(client.Post<TWireFormat, TWireFormat>($"{this.GetResourceName()}", this.MapToWire(data)));
                    this.m_cacheService.Add(retVal);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public virtual TModel Save(TModel data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data is IHasTemplate iht)
            {
                this.UpstreamIntegrationService.HarmonizeTemplateId(iht);
            }
            else if (data is IResourceCollection irc)
            {
                irc.Item.OfType<IHasTemplate>().ToList().ForEach(r => this.UpstreamIntegrationService.HarmonizeTemplateId(r));
            }


            try
            {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    if (!data.Key.HasValue)
                    {
                        data.Key = Guid.NewGuid();
                    }

                    // Create or Update
                    var retVal = this.MapFromWire(client.Post<TWireFormat, TWireFormat>($"{this.GetResourceName()}/{data.Key}", this.MapToWire(data)));
                    this.m_cacheService.Add(retVal);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }

        }

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Get(Guid key) => this.Get(key);

        /// <inheritdoc/>
        IQueryResultSet IRepositoryService.Find(Expression query) => this.Find(query as Expression<Func<TModel, bool>>);

        /// <inheritdoc/>
        [Obsolete("Use Find(Expression)", true)]
        IEnumerable<IdentifiedData> IRepositoryService.Find(Expression query, int offset, int? count, out int totalResults)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Insert(object data) => this.Insert((TModel)data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Save(object data) => this.Insert((TModel)data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Delete(Guid key) => this.Delete(key);

        /// <inheritdoc/>
        public void Touch(Guid key)
        {
            try
            {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    // Create or Update
                    client.Invoke<Object, Object>("TOUCH", $"{this.GetResourceName()}/{key}", null);
                    this.m_cacheService.Remove(key);
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }

        }
    }
}
