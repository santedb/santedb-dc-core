/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-11-23
 */
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using SanteDB.DisconnectedClient.Core.Services;
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
        public List<Claim> ExtractClaims(NameValueCollection headers)
        {
            var claimsHeaders = headers[HeaderTypes.HttpClaims];
            if (claimsHeaders == null)
                return new List<Claim>();
            else
                return claimsHeaders.Split(',').Select(o => Encoding.UTF8.GetString(Convert.FromBase64String(o)).Split('=')).Select(c => new Claim(c[0], c[1])).ToList();
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
        /// Authenticate the user
        /// </summary>
        public SessionInfo Authenticate(NameValueCollection request)
        {
            ISessionManagerService sessionService = ApplicationContext.Current.GetService<ISessionManagerService>();
            SessionInfo retVal = null;

            List<Claim> claims = new List<Claim>()
            {
                new Claim("scope", request["scope"] ?? "*")
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
                    var ses = sessionService.Get(AuthenticationContext.Current.Principal);
                    if (ses.RefreshToken == request["refresh_token"])
                        retVal = ses.Extend(request["refresh_token"]); // Force a re-issue
                    break;
                case "pin":
                    var pinAuthSvc = sessionService as IPinAuthenticationService;
                    retVal = sessionService.Authenticate(request["username"], request["pin"].Select(o => Byte.Parse(o.ToString())).ToArray(), claims.Union(this.ExtractClaims(RestOperationContext.Current.IncomingRequest.Headers)).ToArray());
                    break;
            }

            // override
            if (claims.Any(o => o.Type == ClaimTypes.SanteDBOverrideClaim && o.Value == "true")) {
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
                if (!Boolean.Parse(RestOperationContext.Current.IncomingRequest.Headers[HeaderTypes.HttpUserAccessControlPrompt] ?? "false")) // Requesting all access so we need to send back a session ID :)
                    RestOperationContext.Current.OutgoingResponse.SetCookie(new Cookie("_s", retVal.Token)
                    {
                        HttpOnly = true,
                        Secure = true,
                        Path = "/",
                        Domain = RestOperationContext.Current.IncomingRequest.Url.Host
                    });
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
