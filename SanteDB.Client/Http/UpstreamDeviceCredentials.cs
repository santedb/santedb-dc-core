using SanteDB.Client.Upstream.Management;
using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Services;
using SanteDB.Rest.Common;
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
    public class UpstreamDeviceCredentials : RestRequestCredentials
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

        /// <summary>
        /// Set the credentials for this
        /// </summary>
        public override void SetCredentials(HttpWebRequest webRequest)
        {
            if (this.m_upstreamConfiguration != null)
            {
                var headerValue = Encoding.UTF8.GetBytes($"{this.m_upstreamConfiguration.LocalDeviceName}:{this.m_upstreamConfiguration.LocalDeviceSecret}");
                webRequest.Headers.Add(ExtendedHttpHeaderNames.HttpDeviceCredentialHeaderName, $"BASIC {Convert.ToBase64String(headerValue)}");
            }
        }

    }
}
