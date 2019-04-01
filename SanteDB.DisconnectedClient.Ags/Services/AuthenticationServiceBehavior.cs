/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using Newtonsoft.Json;
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Xamarin.Security;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// Authentication service behavior
    /// </summary>
    [ServiceBehavior(Name = "AUTH", InstanceMode = ServiceInstanceMode.PerCall)]
    public class AuthenticationServiceBehavior : IAuthenticationServiceContract
    {
        private Tracer m_tracer = Tracer.GetTracer(typeof(AuthenticationServiceBehavior));

        /// <summary>
        /// Extract claims
        /// </summary>
        public List<IClaim> ExtractClaims(NameValueCollection headers)
        {
            var claimsHeaders = headers[HeaderTypes.HttpClaims];
            if (claimsHeaders == null)
                return new List<IClaim>();
            else
                return claimsHeaders.Split(',').Select(o => Encoding.UTF8.GetString(Convert.FromBase64String(o)).Split('=')).Select(c => new SanteDBClaim(c[0], c[1])).OfType<IClaim>().ToList();
        }

        /// <summary>
        /// Abandons the session
        /// </summary>
        public void AbandonSession()
        {
            // Get the session
            if (AuthenticationContext.Current.Principal != null) { 
                ApplicationContext.Current.GetService<ISessionManagerService>().Delete(AuthenticationContext.Current.Principal);
                AuditUtil.AuditLogout(AuthenticationContext.Current.Principal);
            }

        }

        /// <summary>
        /// Authenticate OAUTH
        /// </summary>
        public OAuthTokenResponse AuthenticateOAuth(NameValueCollection request)
        {
            try
            {
                var session = this.Authenticate(request);
                return new OAuthTokenResponse()
                {
                    IdToken = session.IdentityToken,
                    AccessToken = session.Token,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = (int)session.Expiry.Subtract(DateTime.Now).TotalSeconds,
                    TokenType = "urn:santedb:session-info"
                };
            }
            catch(Exception e)
            {
                RestOperationContext.Current.OutgoingResponse.StatusCode = 400;
                return new OAuthTokenResponse()
                {
                    Error = "invalid_grant",
                    ErrorDescription = e.Message
                };
            }
        }

        /// <summary>
        /// Authenticate the user
        /// </summary>
        public SessionInfo Authenticate(NameValueCollection request)
        {
            var sessionService = ApplicationContext.Current.GetService<ISessionManagerService>();
            SessionInfo retVal = null;

            List<IClaim> claims = new List<IClaim>()
            {
                new SanteDBClaim("scope", request["scope"] ?? "*")
            };

            switch (request["grant_type"])
            {
                case "password":
                    var tfa = RestOperationContext.Current.IncomingRequest.Headers[HeaderTypes.HttpTfaSecret];
                    if (!String.IsNullOrEmpty(tfa))
                        retVal = sessionService.Authenticate(request["username"], request["password"], tfa);
                    else
                        retVal = sessionService.Authenticate(request["username"], request["password"]);
                    break;
                case "refresh":
                    var ses = sessionService.Refresh(request["refresh_token"]);
                    break;
                case "pin":
                    var pinAuthSvc = sessionService as IPinAuthenticationService;
                    retVal = sessionService.Authenticate(request["username"], request["pin"].Select(o => Byte.Parse(o.ToString())).ToArray(), claims.Union(this.ExtractClaims(RestOperationContext.Current.IncomingRequest.Headers)).ToArray());
                    break;
            }

            // override
            if (claims.Any(o => o.Type == SanteDBClaimTypes.SanteDBOverrideClaim && o.Value == "true")) {
                throw new NotImplementedException(); // TODO:
            }

            if (retVal == null)
                throw new SecurityException();
            else
            {
                var lanugageCode = retVal?.UserEntity?.LanguageCommunication?.FirstOrDefault(o => o.IsPreferred)?.LanguageCode;

                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(CultureInfo.DefaultThreadCurrentUICulture?.TwoLetterISOLanguageName ?? "en");

                if (lanugageCode != null)
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lanugageCode);

                // Set the session 
                //if (!Boolean.Parse(RestOperationContext.Current.IncomingRequest.Headers[HeaderTypes.HttpUserAccessControlPrompt] ?? "false")) // Requesting all access so we need to send back a session ID :)
                //    RestOperationContext.Current.OutgoingResponse.SetCookie(new Cookie("_s", retVal.Token)
                //    {
                //        HttpOnly = true,
                //        Secure = true,
                //        Path = "/",
                //        Domain = RestOperationContext.Current.IncomingRequest.Url.Host
                //    });
                return retVal;
            }
        }

        /// <summary>
        /// Get the specified session information
        /// </summary>
        public SessionInfo GetSession()
        {
            return ApplicationContext.Current.GetService<ISessionManagerService>().Get(AuthenticationContext.Current.Principal.ToString());
        }

        /// <summary>
        /// Perform a pre-check
        /// </summary>
        public void AclPreCheck(string policyId)
        {
            throw new NotImplementedException();
        }
    }
}
