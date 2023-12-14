/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;

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
            return wireFormat?.Entity;
        }

    }
}
