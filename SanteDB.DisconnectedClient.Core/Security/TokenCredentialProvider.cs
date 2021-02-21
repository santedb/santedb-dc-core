/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2021-2-9
 */
using SanteDB.Core.Api.Security;
using SanteDB.Core.Http;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services;
using System;
using System.Security;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents a credential provider which provides a token
    /// </summary>
    public class TokenCredentialProvider : ICredentialProvider
    {
        #region ICredentialProvider implementation
        /// <summary>
        /// Gets or sets the credentials which are used to authenticate
        /// </summary>
        /// <returns>The credentials.</returns>
        /// <param name="context">Context.</param>
        public Credentials GetCredentials(IRestClient context)
        {
            return this.GetCredentials(AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Authenticate a user - this occurs when reauth is required
        /// </summary>
        /// <param name="context">Context.</param>
        public Credentials Authenticate(IRestClient context)
        {

            // TODO: Determine why we're reauthenticating... if it is an expired token we'll need to get the refresh token
            var tokenCredentials = AuthenticationContext.Current.Principal as TokenClaimsPrincipal;
            if (tokenCredentials != null)
            {
                var expiryTime = tokenCredentials.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime();
                if (expiryTime.HasValue && expiryTime < DateTime.Now)
                {
                    var idp = ApplicationContext.Current.GetService<IIdentityProviderService>();
                    var principal = idp.ReAuthenticate(AuthenticationContext.Current.Principal);   // Force a re-issue
                    AuthenticationContext.Current = new AuthenticationContext(principal);
                    //ApplicationContext.Current.SetDefaultPrincipal(principal);
                }
                else if (expiryTime > DateTime.Now) // Token is good?
                    return this.GetCredentials(context);
                else // I don't know what happened
                    throw new SecurityException("Server access token is expired");
            }
            else
                throw new SecurityException("Invalid authentication principal - Offline principal used to access server");
            return null;
        }

        /// <summary>
        /// Get credentials from the specified principal
        /// </summary>
        public Credentials GetCredentials(IPrincipal principal)
        {
            if (principal is TokenClaimsPrincipal)
            {
                return new TokenCredentials(principal);
            }
            else
            {
                // We need a token claims principal
                // TODO: Re-authenticate this user against the ACS
                return new TokenCredentials(AuthenticationContext.Current.Principal);
            }
        }
        #endregion
    }

}

