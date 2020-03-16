/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using Newtonsoft.Json;
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Behaviors;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using SanteDB.DisconnectedClient.Core.Security.Session;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Xamarin.Security;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Authentication Service BEhavior
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
            if (RestOperationContext.Current.Data.TryGetValue(AgsAuthorizationServiceBehavior.SessionPropertyName, out object session))
                ApplicationContext.Current.GetService<ISessionProviderService>().Abandon(session as ISession);
        }

        /// <summary>
        /// Authenticate OAUTH
        /// </summary>
        public OAuthTokenResponse AuthenticateOAuth(NameValueCollection request)
        {
            try
            {
                var sessionService = ApplicationContext.Current.GetService<ISessionProviderService>();
                var identityService = ApplicationContext.Current.GetService<IIdentityProviderService>();
                var remoteEpResolve = ApplicationContext.Current.GetService<IRemoteEndpointResolver>();

                var headerClaims = Encoding.UTF8.GetString(Convert.FromBase64String(RestOperationContext.Current.IncomingRequest.Headers[HeaderTypes.HttpClaims])).Split(';').ToDictionary(o => o.Split('=')[0], o => o.Split('=')[1]);
                ISession session = null;

                var scopes = request["scope"] == "*" ? null : request["scope"].Split(' ');
                var isOverride = scopes?.Any(o => o == PermissionPolicyIdentifiers.OverridePolicyPermission) == true || headerClaims.TryGetValue(SanteDBClaimTypes.SanteDBOverrideClaim, out string overrideFlag) && overrideFlag == "true";
                var purposeOfUse = headerClaims.FirstOrDefault(o => o.Key == SanteDBClaimTypes.PurposeOfUse).Value;
                var tfa = RestOperationContext.Current.IncomingRequest.Headers[HeaderTypes.HttpTfaSecret];

                // TODO: Authenticate the client and device

                // Grant types
                switch (request["grant_type"])
                {
                    case "x_challenge":
                        {
                            var principal = ApplicationServiceContext.Current.GetService<ISecurityChallengeIdentityService>().Authenticate(request["username"], Guid.Parse(request["challenge"]), request["response"], tfa);
                            if (principal != null)
                                session = sessionService.Establish(principal, remoteEpResolve.GetRemoteEndpoint(), isOverride, purposeOfUse, scopes);
                            else
                                throw new SecurityException("Could not authenticate principal");
                            break;
                        }
                    case "password":
                        {
                            IPrincipal principal = null;
                            if (isOverride && identityService is IElevatableIdentityProviderService elevatedAuth)
                                principal = elevatedAuth.ElevatedAuthenticate(request["username"], request["password"], tfa, purposeOfUse, scopes);
                            if (!String.IsNullOrEmpty(tfa))
                                principal = identityService.Authenticate(request["username"], request["password"], tfa);
                            else
                                principal = identityService.Authenticate(request["username"], request["password"]);

                            if (principal != null)
                                session = sessionService.Establish(principal, remoteEpResolve.GetRemoteEndpoint(), isOverride, purposeOfUse, scopes);
                            else
                                throw new SecurityException("Could not authenticate principal");

                        }
                        break;
                    case "refresh_token":
                        {
                            var refreshToken = Enumerable.Range(0, request["refresh_token"].Length)
                                           .Where(x => x % 2 == 0)
                                           .Select(x => Convert.ToByte(request["refresh_token"].Substring(x, 2), 16))
                                           .ToArray();

                            // Get the local session               
                            session = sessionService.Extend(refreshToken);
                            break;
                        }
                    case "pin":
                        {
                            var pinAuthSvc = ApplicationServiceContext.Current.GetService<IPinAuthenticationService>();
                            IPrincipal principal = pinAuthSvc.Authenticate(request["username"], request["pin"].Select(o => Byte.Parse(o.ToString())).ToArray());
                            if (principal != null)
                                session = sessionService.Establish(principal, remoteEpResolve.GetRemoteEndpoint(), isOverride, purposeOfUse, scopes);
                            else
                                throw new SecurityException("Could not authenticate principal");
                            break;
                        }
                }

                var sessionInfo = new SessionInfo(session);
                var lanugageCode = sessionInfo?.UserEntity?.LanguageCommunication?.FirstOrDefault(o => o.IsPreferred)?.LanguageCode;
                if (lanugageCode != null)
                    Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new CultureInfo(CultureInfo.DefaultThreadCurrentUICulture?.TwoLetterISOLanguageName ?? "en");

                return new OAuthTokenResponse()
                {
                    AccessToken = BitConverter.ToString(session.Id).Replace("-", ""),
                    RefreshToken = BitConverter.ToString(session.RefreshToken).Replace("-", ""),
                    ExpiresIn = (int)DateTime.Now.Subtract(session.NotAfter.DateTime).TotalSeconds,
                    TokenType = "bearer",
                    IdToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(session.Claims.GroupBy(o=>o.Type).ToDictionary(o => o.Key, o => o.Count() == 1 ? (object)o.First().Value : o.Select(v=>v.Value).ToArray()))))
                };
                
            }
            catch (Exception e)
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
        /// Get the specified session information
        /// </summary>
        public SessionInfo GetSession()
        {
            if (RestOperationContext.Current.Data.TryGetValue(AgsAuthorizationServiceBehavior.SessionPropertyName, out object session))
                return new SessionInfo(session as ISession);
            return null;
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
