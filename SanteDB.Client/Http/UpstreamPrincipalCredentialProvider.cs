using SanteDB.Core.Http;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Http
{
    /// <summary>
    /// An <see cref="ICredentialProvider"/> which can proper serialize bearer tokens and certificates via <see cref="UpstreamPrincipalCredentials"/>
    /// </summary>
    public class UpstreamPrincipalCredentialProvider : ICredentialProvider
    {
        /// <inheritdoc/>
        public RestRequestCredentials GetCredentials(IRestClient context) => this.GetCredentials(AuthenticationContext.Current.Principal);

        /// <inheritdoc/>
        public RestRequestCredentials GetCredentials(IPrincipal principal) => new UpstreamPrincipalCredentials(principal);
    }
}
