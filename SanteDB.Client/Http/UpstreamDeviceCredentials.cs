using SanteDB.Client.Upstream;
using SanteDB.Core;
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

namespace SanteDB.Client.Http
{
    /// <summary>
    /// Pricipal based credentials
    /// </summary>
    public class UpstreamDeviceCredentials : UpstreamPrincipalCredentials
    {

        // Upstream integration service
        private readonly ConfiguredUpstreamRealmSettings m_upstreamConfiguration;

        /// <summary>
        /// Create new principal credentials
        /// </summary>
        public UpstreamDeviceCredentials(IPrincipal principal) : base(principal)
        {
            this.m_upstreamConfiguration = ApplicationServiceContext.Current.GetService<IUpstreamIntegrationService>() as ConfiguredUpstreamRealmSettings;
        }

        /// <inheritdoc/>
        protected override bool SetCredentials(IIdentity identity, HttpWebRequest webRequest)
        {
            if(!base.SetCredentials(identity, webRequest) && identity is IDeviceIdentity deviceIdentity)
            {
                if (deviceIdentity.Name.Equals(this.m_upstreamConfiguration.LocalDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    var headerValue = Encoding.UTF8.GetBytes($"{this.m_upstreamConfiguration.LocalDeviceName}:{this.m_upstreamConfiguration.LocalDeviceSecret}");
                    webRequest.Headers.Add(SanteDBClaimTypes.BasicHttpClientClaimHeaderName, $"BAISC {Convert.ToBase64String(headerValue)}");
                    return true;
                }
                else
                {
                    throw new InvalidOperationException(ErrorMessages.PRINCIPAL_NOT_APPROPRIATE);
                }
            }
            else
            {
                return false;
            }
        }
    }
}
