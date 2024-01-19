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
using DocumentFormat.OpenXml.Drawing.Charts;
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
        private readonly IAdhocCacheService m_adhocCacheService;

        /// <summary>
        /// Timeout, in milliseconds, for the ping to complete for the endpoint.
        /// </summary>
        private const int PING_TIMEOUT = 5_000;
        private readonly TimeSpan CACHE_TIMEOUT = new TimeSpan(0, 0, 60);

        

        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "Upstream Availability Provider";

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUpstreamAvailabilityProvider(INetworkInformationService networkInformationService,
            IUpstreamManagementService upstreamManagementService,
            IRestClientFactory restClientFactory,
            IAdhocCacheService adhocCacheService = null)
        {
            this.m_networkInformationService = networkInformationService;
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamManagementService = upstreamManagementService;
            this.m_adhocCacheService = adhocCacheService;
        }


        /// <inheritdoc/>
        public bool IsAvailable(ServiceEndpointType endpoint)
        {
            return GetUpstreamLatency(endpoint).HasValue;
        }

        /// <summary>
        /// Issues an HTTP PING request to the service endpoint and captures latency and time drift reported by the service.
        /// </summary>
        /// <param name="endpoint">The type of endpoint to create an <see cref="IRestClient"/> from. Services are not guaranteed to have similar parameters.</param>
        /// <param name="latencyMs">The latency, in milliseconds, that the request took. This expects that the server will respond quickly.</param>
        /// <param name="drift">The time difference between the local clock and the time reported by the server.</param>
        /// <returns>True if the call succeeded, false otherwise.</returns>
        protected virtual bool GetTimeDriftAndLatencyInternal(ServiceEndpointType endpoint, out long? latencyMs, out TimeSpan? drift)
        {
            using (var client = m_restClientFactory.GetRestClientFor(endpoint))
            {
                client.SetTimeout(PING_TIMEOUT);
                var success = client.TryPing(out var latency, out var timedrift);

                if (success)
                {
                    drift = timedrift;
                    latencyMs = latency;
                    this.m_adhocCacheService?.Add($"us.drift.{endpoint}", drift, CACHE_TIMEOUT);
                    this.m_adhocCacheService?.Add($"us.latency.{endpoint}", latencyMs, CACHE_TIMEOUT);
                }
                else
                {
                    drift = null;
                    latencyMs = null;
                }

                return success;
            }
        }

        /// <inheritdoc/>
        public TimeSpan? GetTimeDrift(ServiceEndpointType endpoint)
        {
            try
            {
                TimeSpan? retVal = null;
                if (this.m_networkInformationService.IsNetworkAvailable &&
                    this.m_networkInformationService.IsNetworkConnected &&
                    this.m_upstreamManagementService.IsConfigured() &&
                    this.m_adhocCacheService?.TryGet($"us.drift.{endpoint}", out retVal) != true)
                {
                    if (!GetTimeDriftAndLatencyInternal(endpoint, out _, out retVal))
                    {
                        return null;
                    }
                }
                return retVal;
            }
            catch(TimeoutException)
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public long? GetUpstreamLatency(ServiceEndpointType endpoint)
        {
            try
            {
                long? retVal = null;
                if (this.m_networkInformationService.IsNetworkAvailable &&
                    this.m_networkInformationService.IsNetworkConnected &&
                    this.m_upstreamManagementService.IsConfigured() &&
                    this.m_adhocCacheService?.TryGet($"us.latency.{endpoint}", out retVal) != true)
                {
                    if (!GetTimeDriftAndLatencyInternal(endpoint, out retVal, out _))
                    {
                        return null;
                    }
                }
                return retVal;
            }
            catch (TimeoutException)
            {
                return null;
            }
        }
    }
}
