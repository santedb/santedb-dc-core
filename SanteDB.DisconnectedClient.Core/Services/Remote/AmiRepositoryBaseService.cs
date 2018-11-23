using SanteDB.Core.Http;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Core.Services.Remote
{
    /// <summary>
    /// Represetns a base service that uses the AMI
    /// </summary>
    public abstract class AmiRepositoryBaseService
    {
        // Service client
        protected AmiServiceClient m_client = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
        protected IPrincipal m_cachedCredential = null;

        /// <summary>
        /// Gets current credentials
        /// </summary>
        protected Credentials GetCredentials()
        {
            var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            AuthenticationContext.Current = new AuthenticationContext(this.m_cachedCredential ?? AuthenticationContext.Current.Principal);

            if (!AuthenticationContext.Current.Principal.Identity.IsAuthenticated ||
                ((AuthenticationContext.Current.Principal as ClaimsPrincipal)?.FindClaim(ClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MinValue) < DateTime.Now)
            {
                AuthenticationContext.Current = new AuthenticationContext(ApplicationContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret));
                this.m_cachedCredential = AuthenticationContext.Current.Principal;
            }
            return this.m_client.Client.Description.Binding.Security.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);
        }
    }
}