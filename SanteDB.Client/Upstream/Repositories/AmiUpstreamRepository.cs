using SanteDB.Core.Http;
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
    internal class AmiUpstreamRepository<TModel> : UpstreamRepositoryServiceBase<TModel, AmiCollection>
        where TModel : IdentifiedData, new()
    {

        /// <summary>
        /// DI constructor
        /// </summary>
        public AmiUpstreamRepository(ILocalizationService localizationService, IDataCachingService cacheService, IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamIntegrationService upstreamIntegrationService = null) : base(ServiceEndpointType.AdministrationIntegrationService, localizationService, cacheService, restClientFactory, upstreamManagementService, upstreamIntegrationService)
        {
        }
    }
}
