using DocumentFormat.OpenXml.EMMA;
using SanteDB.Core.Http;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl.Repository;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// Upstream repository for protocols
    /// </summary>
    public class UpstreamProtocolRepository : HdsiUpstreamRepository<SanteDB.Core.Model.Acts.Protocol>
    {
        /// <summary>
        /// Upstream for protocols
        /// </summary>
        public UpstreamProtocolRepository(ILocalizationService localizationService, IDataCachingService cacheService, IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamIntegrationService upstreamIntegrationService, IAdhocCacheService adhocCacheService, IUpstreamAvailabilityProvider upstreamAvailability) : base(localizationService, cacheService, restClientFactory, upstreamManagementService, upstreamIntegrationService, adhocCacheService, upstreamAvailability)
        {
        }
    }
}
