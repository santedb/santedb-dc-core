using SanteDB.Client.Http;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Messaging.HDSI.Client;
using System;
using System.Security.Principal;

namespace SanteDB.Client.Repositories
{
    /// <summary>
    /// A base class for repositories which use the AMI as their base data source
    /// </summary>
    public class UpstreamServiceBase
    {
        private readonly IRestClientFactory m_restClientFactory;
        private IUpstreamIntegrationService m_upstreamIntegrationService;

        /// <summary>
        /// Get whether the upstream is conifgured 
        /// </summary>
        protected bool IsUpstreamConfigured => this.m_upstreamIntegrationService != null;

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamServiceBase(IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamIntegrationService upstreamIntegrationService = null)
        {
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
            upstreamManagementService.RealmChanged += (o, e) => this.m_upstreamIntegrationService = e.UpstreamIntegrationService;
        }

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
            var client = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService);
            if (authenticatedAs == null)
            {
                client.Credentials = new UpstreamPrincipalCredentials(authenticatedAs);
            }
            else if (AuthenticationContext.Current.Principal == AuthenticationContext.SystemPrincipal && this.m_upstreamIntegrationService != null) // We are the system - so we need to auth as the device
            {
                client.Credentials = new UpstreamPrincipalCredentials(this.m_upstreamIntegrationService.AuthenticateAsDevice());
            }
            else
            {
                client.Credentials = new UpstreamPrincipalCredentials(AuthenticationContext.Current.Principal);
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