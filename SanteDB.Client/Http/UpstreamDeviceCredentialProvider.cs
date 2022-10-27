using SanteDB.Core.Http;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Http
{
    /// <summary>
    /// A <see cref="ICredentialProvider"/> that uses <see cref="UpstreamDeviceCredentials"/> for HTTP rest requests
    /// </summary>
    public class UpstreamDeviceCredentialProvider : ICredentialProvider
    {

        /// <summary>
        /// Get credentials on the specified <paramref name="context"/>
        /// </summary>
        public Credentials GetCredentials(IRestClient context) => this.GetCredentials(AuthenticationContext.Current.Principal);

        /// <summary>
        /// Get credentials for the specified principal
        /// </summary>
        public Credentials GetCredentials(IPrincipal principal) => new UpstreamDeviceCredentials(principal);
    }
}
