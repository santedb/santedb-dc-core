using SanteDB.Core.Http;
using SanteDB.Core.Security.Principal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Http
{
    /// <summary>
    /// Represents a credential 
    /// </summary>
    public class UpstreamPrincipalCredentials : Credentials
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
            if (this.Principal is ClaimsPrincipal claimsPrincipal)
            {
                claimsPrincipal.Identities.ToList().ForEach(o => this.SetCredentials(o, webRequest));
            }
            else
            {
                this.SetCredentials(this.Principal.Identity, webRequest);
            }
        }

        /// <summary>
        /// Set credentials on the <paramref name="webRequest"/>
        /// </summary>
        /// <param name="identity">The identity to set</param>
        /// <param name="webRequest">The web request to update</param>
        protected virtual bool SetCredentials(IIdentity identity, HttpWebRequest webRequest)
        {
            switch (identity)
            {
                case ICertificateIdentity certificateIdentity:
                    webRequest.ClientCertificates.Add(certificateIdentity.AuthenticationCertificate);
                    return true ;
                case ITokenIdentity tokenIdentity:
                    webRequest.Headers.Add(HttpRequestHeader.Authorization, $"{tokenIdentity.AccessTokenType} {tokenIdentity.AccessToken}");
                    return true;
            }
            return false;
        }
    }
}
