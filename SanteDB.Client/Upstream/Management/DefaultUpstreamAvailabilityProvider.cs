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
using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Services;
using System;
using System.Diagnostics;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// Upstream availability provider
    /// </summary>
    public class DefaultUpstreamAvailabilityProvider : IUpstreamAvailabilityProvider
    {
        private readonly INetworkInformationService m_networkInformationService;
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IUpstreamManagementService m_upstreamManagementService;

        /// <summary>
        /// Timeout, in milliseconds, for the ping to complete for the endpoint.
        /// </summary>
        private const int PING_TIMEOUT = 5_000;


        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "Upstream Availability Provider";

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUpstreamAvailabilityProvider(INetworkInformationService networkInformationService,
            IUpstreamManagementService upstreamManagementService,
            IRestClientFactory restClientFactory)
        {
            this.m_networkInformationService = networkInformationService;
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamManagementService = upstreamManagementService;
        }


        /// <inheritdoc/>
        public bool IsAvailable(ServiceEndpointType endpoint)
        {
            return GetUpstreamLatency(endpoint) != -1;
            //if (this.m_networkInformationService.IsNetworkAvailable &&
            //    this.m_networkInformationService.IsNetworkConnected &&
            //    this.m_upstreamManagementService.IsConfigured())
            //{
            //    using (var restClient = m_restClientFactory.GetRestClientFor(endpoint))
            //    {
            //        try
            //        {
            //            restClient.SetTimeout(PING_TIMEOUT);
            //            restClient.Invoke<object, object>("PING", "/", null);
            //            return true;
            //        }
            //        catch
            //        {
            //            return false;
            //        }
            //    }
            //}
            //return false;
        }

        /// <inheritdoc/>
        public TimeSpan? GetTimeDrift(ServiceEndpointType endpoint)
        {
            try
            {
                if (this.m_networkInformationService.IsNetworkAvailable &&
                 this.m_networkInformationService.IsNetworkConnected &&
                 this.m_upstreamManagementService.IsConfigured())
                {
                    using (var client = m_restClientFactory.GetRestClientFor(endpoint))
                    {
                        client.SetTimeout(PING_TIMEOUT);
                        var serverTime = DateTime.Now;
                        client.Responded += (o, e) => _ = DateTime.TryParse(e.Headers["X-GeneratedOn"], out serverTime) || DateTime.TryParse(e.Headers["Date"], out serverTime);
                        client.Invoke<object, object>("PING", "/", null);
                        return serverTime.Subtract(DateTime.Now);
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public long? GetUpstreamLatency(ServiceEndpointType endpointType)
        {
            try
            {
                if (this.m_networkInformationService.IsNetworkAvailable &&
                    this.m_networkInformationService.IsNetworkConnected &&
                    this.m_upstreamManagementService.IsConfigured())
                {
                    using (var client = m_restClientFactory.GetRestClientFor(endpointType))
                    {
                        client.SetTimeout(PING_TIMEOUT);
                        var sw = new Stopwatch();
                        sw.Start();
                        client.Invoke<object, object>("PING", "/", null);
                        sw.Stop();
                        return sw.ElapsedMilliseconds;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
