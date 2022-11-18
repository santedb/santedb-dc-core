using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// Upstream availability provider
    /// </summary>
    public class DefaultUpstreamAvailabilityProvider : IUpstreamAvailabilityProvider
    {
        private readonly INetworkInformationService m_networkInformationService;
        private readonly IRestClientFactory m_restClientFactory;

        /// <summary>
        /// Timeout, in milliseconds, for the ping to complete for the endpoint.
        /// </summary>
        private const int PING_TIMEOUT = 5_000;

        public IUpstreamManagementService m_upstreamManagementService { get; }

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
        public long GetUpstreamLatency(ServiceEndpointType endpointType)
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
                return -1;
            }
            catch 
            {
                return -1 ;
            }
        }
    }
}
