using SanteDB.Client.Configuration;
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Services;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Upstream
{
    /// <summary>
    /// Update manager which uses the AMI to get updates for packages
    /// </summary>
    public class RemoteUpdateManager : IUpdateManager
    {
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IUpstreamIntegrationService m_upstreamIntegrationService;
        private readonly ClientConfigurationSection m_configuration;
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Remote Applet Update Manager";

        /// <summary>
        /// DI constructor
        /// </summary>
        public RemoteUpdateManager(IRestClientFactory restClientFactory, 
            IUpstreamIntegrationService upstreamIntegrationService, 
            IConfigurationManager configurationManager,
            ILocalizationService localizationService)
        {
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
            this.m_configuration = configurationManager.GetSection<ClientConfigurationSection>();
            this.m_localizationService = localizationService ;
        }

        /// <inheritdoc/>
        public AppletInfo GetServerInfo(string packageId)
        {
            try
            {
                using(AuthenticationContext.EnterContext(this.m_upstreamIntegrationService.AuthenticateAsDevice()))
                {
                    var restClient = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService);
                    var headers = restClient.Head($"AppletSolution/{this.m_configuration.UiSolution}/applet/{packageId}");
                    headers.TryGetValue("X-SanteDB-PakID", out string packId);
                    headers.TryGetValue("ETag", out string versionKey);
                    return new AppletInfo
                    {
                        Id = packageId,
                        Version = versionKey
                    };
                }
            }
            catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { resource = $"applet/{packageId}" }), e); 
            }
        }

        public void Install(string packageId)
        {
            throw new NotImplementedException();
        }

        public void Update(bool nonInteractive)
        {
            throw new NotImplementedException();
        }
    }
}
