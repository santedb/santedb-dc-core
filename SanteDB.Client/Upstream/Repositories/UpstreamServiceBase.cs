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
using SanteDB.Client.Http;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Messaging.HDSI.Client;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// A base class for repositories which use the AMI as their base data source
    /// </summary>
    public abstract class UpstreamServiceBase
    {
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IUpstreamAvailabilityProvider m_upstreamAvailabilityProvider;
        private readonly IUpstreamIntegrationService m_upstreamIntegrationService;
        private readonly IUpstreamManagementService m_upstreamManagementService;

        /// <summary>
        /// The tracer to use to log messages
        /// </summary>
        protected readonly Tracer _Tracer;

        /// <summary>
        /// Get whether the upstream is conifgured 
        /// </summary>
        protected bool IsUpstreamConfigured => this.m_upstreamManagementService.IsConfigured();

        /// <summary>
        /// Get the upstream management service
        /// </summary>
        protected IUpstreamIntegrationService UpstreamIntegrationService => this.m_upstreamIntegrationService;

        /// <summary>
        /// Get the upstream availability provider
        /// </summary>
        protected IUpstreamAvailabilityProvider UpstreamAvailabilityProvider => this.m_upstreamAvailabilityProvider;

        /// <summary>
        /// Get the upstream client factory
        /// </summary>
        protected IRestClientFactory RestClientFactory => this.m_restClientFactory;

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamServiceBase(IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService = null)
        {
            this._Tracer = new Tracer(GetType().Name); //Not nameof so that the non-abstract type is used.
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamAvailabilityProvider = upstreamAvailabilityProvider;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
            this.m_upstreamManagementService = upstreamManagementService;
        }

        /// <summary>
        /// Gets a value that indicates whether the upstream 
        /// </summary>
        /// <param name="endpointType"></param>
        /// <returns></returns>
        public bool IsUpstreamAvailable(Core.Interop.ServiceEndpointType endpointType)
            => IsUpstreamConfigured && this.m_upstreamAvailabilityProvider.IsAvailable(endpointType);

        /// <summary>
        /// Get client for the AMI 
        /// </summary>
        protected AmiServiceClient CreateAmiServiceClient(IPrincipal authenticatedAs = null)
        {
            return new AmiServiceClient(this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, authenticatedAs));
        }

        /// <summary>
        /// Create a generic rest client
        /// </summary>
        protected IRestClient CreateRestClient(ServiceEndpointType serviceEndpointType, IPrincipal authenticatedAs)
        {
            var client = this.m_restClientFactory.GetRestClientFor(serviceEndpointType);
            authenticatedAs = authenticatedAs ?? AuthenticationContext.Current.Principal;

            if((AuthenticationContext.Current.Principal == AuthenticationContext.SystemPrincipal ||
                AuthenticationContext.Current.Principal == AuthenticationContext.AnonymousPrincipal) && this.m_upstreamIntegrationService != null)
            {
                client.Credentials = new UpstreamPrincipalCredentials(this.m_upstreamIntegrationService.AuthenticateAsDevice());
            }
            else 
            {
                client.Credentials = new UpstreamPrincipalCredentials(authenticatedAs);
            }
            return client;
        }

        /// <summary>
        /// Create an HDSI service client
        /// </summary>
        protected HdsiServiceClient CreateHdsiServiceClient(IPrincipal authenticatedAs = null)
        {
            return new HdsiServiceClient(this.CreateRestClient(ServiceEndpointType.HealthDataService, authenticatedAs));
        }

    }
}