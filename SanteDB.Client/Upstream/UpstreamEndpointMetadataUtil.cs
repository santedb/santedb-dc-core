﻿/*
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
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Client.Upstream
{
    /// <summary>
    /// Upstream endpoint metadat utility
    /// </summary>
    public class UpstreamEndpointMetadataUtil : UpstreamServiceBase
    {

        private static UpstreamEndpointMetadataUtil s_current = null;
        private static readonly object s_lock = new object();

        private readonly IDictionary<String, ServiceEndpointType> m_serviceEndpoints;

        /// <summary>
        /// Creates a new instance of the metadata utility
        /// </summary>
        private UpstreamEndpointMetadataUtil(IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            using (var amiClient = this.CreateRestClient(ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
            {
                var options = amiClient.Options<ServiceOptions>("/");
                this.m_serviceEndpoints = options.Endpoints.SelectMany(e =>
                {
                    try
                    {
                        using (var client = this.CreateRestClient(e.ServiceType, AuthenticationContext.Current.Principal))
                        {
                            return client.Options<ServiceOptions>("/").Resources.Select(o => new KeyValuePair<String, ServiceEndpointType>(o.ResourceName, e.ServiceType));
                        }
                    }
                    catch
                    {
                        return new KeyValuePair<String, ServiceEndpointType>[0];
                    }
                }).ToDictionaryIgnoringDuplicates(o => o.Key, o => o.Value);
            }
        }

        /// <summary>
        /// Get the singleton instance
        /// </summary>
        public static UpstreamEndpointMetadataUtil Current
        {
            get
            {
                if (s_current == null)
                {
                    lock (s_lock)
                    {
                        if (s_current == null)
                        {
                            var sp = ApplicationServiceContext.Current.GetService<IServiceProvider>();
                            s_current = new UpstreamEndpointMetadataUtil(sp.GetService<IRestClientFactory>(),
                                sp.GetService<IUpstreamManagementService>(),
                                sp.GetService<IUpstreamAvailabilityProvider>(),
                                sp.GetService<IUpstreamIntegrationService>());
                        }
                    }
                }
                return s_current;
            }
        }

        /// <summary>
        /// Get all supported resource types on the specified <paramref name="serviceEndpoint"/>
        /// </summary>
        public IEnumerable<String> GetSupportedResources(ServiceEndpointType serviceEndpoint) => this.m_serviceEndpoints.Where(o => o.Value == serviceEndpoint).Select(o => o.Key);

        /// <summary>
        /// Get service endpoint
        /// </summary>
        public ServiceEndpointType GetServiceEndpoint<T>() => this.GetServiceEndpoint(typeof(T));

        /// <summary>
        /// Get the service endpoint which services <paramref name="t"/>
        /// </summary>
        public ServiceEndpointType GetServiceEndpoint(Type t)
        {
            return this.m_serviceEndpoints.TryGetValue(t.GetSerializationName(), out var retVal) ? retVal : ServiceEndpointType.Other;
        }
    }
}
