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
    public class UpstreamDeviceCredentials : UpstreamPrincipalCredentials
    {

        // Upstream integration service
        private static ConfiguredUpstreamRealmSettings m_upstreamConfiguration;

        static UpstreamDeviceCredentials()
        {
            ApplicationServiceContext.Current.GetService<IUpstreamManagementService>().RealmChanging += (o, e) => m_upstreamConfiguration = e.UpstreamRealmSettings as ConfiguredUpstreamRealmSettings;
        }

        /// <summary>
        /// Create new principal credentials
        /// </summary>
        public UpstreamDeviceCredentials(IPrincipal principal) : base(principal)
        {
        }

        /// <summary>
        /// Set the credentials for this
        /// </summary>
        public override void SetCredentials(HttpWebRequest webRequest)
        {
            if (m_upstreamConfiguration != null)
            {
                var headerValue = Encoding.UTF8.GetBytes($"{m_upstreamConfiguration.LocalDeviceName}:{m_upstreamConfiguration.LocalDeviceSecret}");
                webRequest.Headers.Add(ExtendedHttpHeaderNames.HttpDeviceCredentialHeaderName, $"BASIC {Convert.ToBase64String(headerValue)}");
            }
            else
            {
                base.SetCredentials(webRequest);
            }
        }

    }
}
