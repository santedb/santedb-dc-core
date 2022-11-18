using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// Wrapped upstream repository for AMI which uses the ISecurityEntityWrapper
    /// </summary>
    internal class AmiWrappedUpstreamRepository<TModel, TWrapper> : UpstreamRepositoryServiceBase<TModel, TWrapper, AmiCollection>
        where TModel : NonVersionedEntityData, new()
        where TWrapper : class, ISecurityEntityInfo<TModel>, new()
    {
        public AmiWrappedUpstreamRepository(ILocalizationService localizationService, IDataCachingService cacheService, IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, IUpstreamIntegrationService upstreamIntegrationService, IAdhocCacheService adhocCacheService = null) : base(ServiceEndpointType.AdministrationIntegrationService, localizationService, cacheService, restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService, adhocCacheService)
        {
        }

        /// <inheritdoc/>
        protected override TWrapper MapToWire(TModel modelObject)
        {
            var retVal = new TWrapper() { Entity = modelObject };
            if (modelObject.Key.HasValue)
            {
                ((IAmiIdentified)retVal).Key = modelObject.Key;
                ((IIdentifiedResource)retVal).Key = modelObject.Key;
            }
            return retVal;
        }

        /// <inheritdoc/>
        protected override TModel MapFromWire(TWrapper wireFormat)
        {
            return wireFormat.Entity;
        }

    }
}
