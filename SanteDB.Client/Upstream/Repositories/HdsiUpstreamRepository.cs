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
 * Date: 2023-3-10
 */
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
