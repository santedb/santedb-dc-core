/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using DocumentFormat.OpenXml.Spreadsheet;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.Client.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// A metadata repository that uses the upstream
    /// </summary>
    public class UpstreamBiMetadataRepository : UpstreamServiceBase, IBiMetadataRepository
    {
        private readonly IAdhocCacheService m_adhocCache;
        private readonly ILocalizationService m_localeService;
        private readonly TimeSpan CACHE_TIMEOUT = new TimeSpan(0, 1, 0);

        /// <summary>
        /// DI ctor
        /// </summary>
        public UpstreamBiMetadataRepository(IAdhocCacheService adhocCache, ILocalizationService localeService, IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_adhocCache = adhocCache;
            this.m_localeService = localeService;
        }

        /// <inheritdoc/>
        public bool IsLocal => false;

        /// <inheritdoc/>
        public string ServiceName => "Upstream BI Metadata Repository";

        /// <inheritdoc/>
        public TBisDefinition Get<TBisDefinition>(string id) where TBisDefinition : BiDefinition, new()
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            try
            {
                using(var rc = base.CreateRestClient(Core.Interop.ServiceEndpointType.BusinessIntelligenceService, AuthenticationContext.Current.Principal))
                {
                    // TODO: CACHE
                    var cacheKey = $"{typeof(TBisDefinition).GetSerializationName()}/{id}";
                    if (!this.m_adhocCache.TryGet<TBisDefinition>(cacheKey, out var retVal))
                    {
                        retVal = rc.Get<TBisDefinition>(cacheKey);
                        this.m_adhocCache.Add(cacheKey, retVal, CACHE_TIMEOUT);
                    }
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public TBisDefinition Insert<TBisDefinition>(TBisDefinition metadata) where TBisDefinition : BiDefinition, new()
        {
            if(metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            try
            {
                using(var rc = this.CreateRestClient(Core.Interop.ServiceEndpointType.BusinessIntelligenceService, AuthenticationContext.Current.Principal))
                {
                    var retVal = rc.Post<TBisDefinition, TBisDefinition>(typeof(TBisDefinition).GetSerializationName(), metadata);
                    var cacheKey = $"{typeof(TBisDefinition).GetSerializationName()}/{retVal.Id}";
                    this.m_adhocCache.Remove(cacheKey);
                    return retVal;

                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = metadata }), e);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<TBisDefinition> Query<TBisDefinition>(Expression<Func<TBisDefinition, bool>> filter, int offset, int? count) where TBisDefinition : BiDefinition, new()
        {
            return this.Query(filter).Skip(offset).Take(count ?? Int32.MaxValue);
        }

        /// <inheritdoc/>
        public IQueryResultSet<TBisDefinition> Query<TBisDefinition>(Expression<Func<TBisDefinition, bool>> filter) where TBisDefinition : BiDefinition, new()
        {
            if(filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            return new UpstreamQueryResultSet<TBisDefinition, BiDefinitionCollection>(this.CreateRestClient(Core.Interop.ServiceEndpointType.BusinessIntelligenceService, AuthenticationContext.Current.Principal), filter);
        }

        /// <inheritdoc/>
        public void Remove<TBisDefinition>(string id) where TBisDefinition : BiDefinition, new()
        {
            if(String.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(id);
            }

            try
            {
                using(var rc = this.CreateRestClient(Core.Interop.ServiceEndpointType.BusinessIntelligenceService, AuthenticationContext.Current.Principal))
                {
                    var cacheKey = $"{typeof(TBisDefinition).GetSerializationName()}/{id}";
                    var retVal = rc.Delete<TBisDefinition>(cacheKey);
                    this.m_adhocCache.Remove(cacheKey);
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = id }), e);
            }
        }
    }
}
