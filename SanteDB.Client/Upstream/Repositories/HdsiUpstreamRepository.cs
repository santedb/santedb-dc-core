using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// HDSI upstream repository
    /// </summary>
    internal class HdsiUpstreamRepository<TModel> : UpstreamRepositoryServiceBase<TModel, TModel, Bundle>
        where TModel : IdentifiedData, new()
    {

        /// <summary>
        /// DI constructor
        /// </summary>
        public HdsiUpstreamRepository(ILocalizationService localizationService, IDataCachingService cacheService, IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService, 
            IUpstreamIntegrationService upstreamIntegrationService,
            IAdhocCacheService adhocCacheService,
            IUpstreamAvailabilityProvider upstreamAvailability
            ) 
            : base(ServiceEndpointType.HealthDataService, localizationService, cacheService, restClientFactory, upstreamManagementService, upstreamAvailability, upstreamIntegrationService, adhocCacheService)
        {
        }

        /// <summary>
        /// Get whether the upstream is available
        /// </summary>
        protected bool IsUpstreamAvailable() => this.IsUpstreamAvailable(ServiceEndpointType.HealthDataService);
    }
}
