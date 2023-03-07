using SanteDB.Client.OAuth;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Http
{
    /// <summary>
    /// Represents a credential 
    /// </summary>
    public class UpstreamPrincipalCredentials : RestRequestCredentials
    {

        /// <summary>
        /// Create upstream credentials 
        /// </summary>
        public UpstreamPrincipalCredentials(IPrincipal principal) : base(principal)
        {
        }

        /// <inheritdoc/>
        public override void SetCredentials(HttpWebRequest webRequest)
        {
            switch(this.Principal)
            {
                case ITokenPrincipal itp:
                    webRequest.Headers.Add(HttpRequestHeader.Authorization, $"{itp.TokenType} {itp.AccessToken}");
                    break;
                case ICertificatePrincipal icp:
                    if(!webRequest.ClientCertificates.Contains(icp.AuthenticationCertificate))
                    {
                        webRequest.ClientCertificates.Add(icp.AuthenticationCertificate);
                    }
                    break;
                
            }
        }

    }
}
