using SanteDB.Client.Upstream;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Core.Http
{
    /// <summary>
    /// Pricipal based credentials
    /// </summary>
    public class UpstreamDeviceCredentials : Credentials
    {

        // Upstream integration service
        private readonly ConfiguredUpstreamRealmSettings m_upstreamConfiguration;

        /// <summary>
        /// Create new principal credentials
        /// </summary>
        public UpstreamDeviceCredentials(IPrincipal principal ) : base(principal) {
            this.m_upstreamConfiguration = ApplicationServiceContext.Current.GetService<IUpstreamIntegrationService>() as ConfiguredUpstreamRealmSettings;
        }

        /// <summary>
        /// Set the credentials for the web request
        /// </summary>
        /// <param name="webRequest">The request to set credentials on</param>
        public override void SetCredentials(HttpWebRequest webRequest)
        {
            if(this.Principal is IClaimsPrincipal claimsPrincipal)
            {
                if(claimsPrincipal.TryGetClaimValue(SanteDBClaimTypes.AuthenticationCertificate, out var subjectName))
                {
                    var clientCertificate = X509CertificateUtils.FindCertificate(X509FindType.FindBySubjectName, StoreLocation.CurrentUser, StoreName.My, subjectName);
                    if(clientCertificate == null)
                    {
                        throw new InvalidOperationException(ErrorMessages.CERTIFICATE_NOT_FOUND);
                    }
                    webRequest.ClientCertificates.Add(clientCertificate);
                    return;
                }

                var deviceIdentity = claimsPrincipal.Identities.OfType<IDeviceIdentity>().FirstOrDefault();
                if(deviceIdentity == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, typeof(IDeviceIdentity)));
                }
                else if(deviceIdentity.Name.Equals(this.m_upstreamConfiguration.LocalDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    var headerValue = Encoding.UTF8.GetBytes($"{this.m_upstreamConfiguration.LocalDeviceName}:{this.m_upstreamConfiguration.LocalDeviceSecret}");
                    webRequest.Headers.Add(SanteDBClaimTypes.BasicHttpClientClaimHeaderName, $"BAISC {Convert.ToBase64String(headerValue)}");
                }
                else
                {
                    throw new SecurityException(ErrorMessages.PRINCIPAL_NOT_APPROPRIATE);
                }
            }
        }
    }
}
