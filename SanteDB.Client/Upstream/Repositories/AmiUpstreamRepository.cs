﻿using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// HDSI upstream repository
    /// </summary>
    internal class AmiUpstreamRepository<TModel> : UpstreamRepositoryServiceBase<TModel, TModel, AmiCollection>
        where TModel : IdentifiedData, new()
    {

        /// <summary>
        /// DI constructor
        /// </summary>
        public AmiUpstreamRepository(ILocalizationService localizationService, IDataCachingService cacheService, IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailability,
            IUpstreamIntegrationService upstreamIntegrationService,
            IAdhocCacheService adhocCacheService = null)
            : base(ServiceEndpointType.AdministrationIntegrationService, localizationService, cacheService, restClientFactory, upstreamManagementService, upstreamAvailability, upstreamIntegrationService, adhocCacheService)
        {
        }

        /// <summary>
        /// Get whether the upstream is available
        /// </summary>
        protected bool IsUpstreamAvailable() => this.IsUpstreamAvailable(ServiceEndpointType.AdministrationIntegrationService);

    }
}