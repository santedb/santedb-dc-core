/*
 * Based on OpenIZ, Copyright (C) 2015 - 2020 Mohawk College of Applied Arts and Technology
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
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Security.Audit;
using SanteDB.DisconnectedClient.Security.Session;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Security;
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
using SanteDB.Core.Model;
using SanteDB.Core.Api.Security;
using SanteDB.Core.Http;
using SanteDB.Core.Exceptions;
using SanteDB.DisconnectedClient.Configuration;

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
        /// Throw exception if not running
        /// </summary>
        private void ThrowIfNotRunning()
        {
            if (!ApplicationServiceContext.Current.IsRunning)
                throw new DomainStateException();
        }

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
            this.ThrowIfNotRunning();
            // Get the session
            if (RestOperationContext.Current.Data.TryGetValue(AgsAuthorizationServiceBehavior.SessionPropertyName, out object session))
            {
                RestOperationContext.Current.OutgoingResponse.SetCookie(new Cookie("_s", ""));
                ApplicationContext.Current.GetService<ISessionProviderService>().Abandon(session as ISession);
            }
        }

        /// <summary>
        /// Authenticate OAUTH
        /// </summary>
        public OAuthTokenResponse AuthenticateOAuth(NameValueCollection request)
        {
            this.ThrowIfNotRunning();

            try
            {
                var sessionService = ApplicationContext.Current.GetService<ISessionProviderService>();
                var identityService = ApplicationContext.Current.GetService<IIdentityProviderService>();
                var remoteEp = RemoteEndpointUtil.Current.GetRemoteClient()?.RemoteAddress;

                var claimsHeader = RestOperationContext.Current.IncomingRequest.Headers[HeaderTypes.HttpClaims];
                IDictionary<String, String> headerClaims = null;
                if (!String.IsNullOrEmpty(claimsHeader))
                    headerClaims = Encoding.UTF8.GetString(Convert.FromBase64String(claimsHeader)).Split(';').ToDictionary(o => o.Split('=')[0], o => o.Split('=')[1]);

                ISession session = null;

                var scopes = request["scope"] == "*" ? null : request["scope"]?.Split(' ');
                var isOverride = scopes?.Any(o => o == PermissionPolicyIdentifiers.OverridePolicyPermission) == true || headerClaims != null && headerClaims.TryGetValue(SanteDBClaimTypes.SanteDBOverrideClaim, out string overrideFlag) && overrideFlag == "true";
                var purposeOfUse = headerClaims?.FirstOrDefault(o => o.Key == SanteDBClaimTypes.PurposeOfUse).Value;
                var tfa = RestOperationContext.Current.IncomingRequest.Headers[HeaderTypes.HttpTfaSecret];
                var signatureService = ApplicationServiceContext.Current.GetService<IDataSigningService>();

                // Grant types
                switch (request["grant_type"])
                {
                    case "x_challenge":
                        {
                            var principal = ApplicationServiceContext.Current.GetService<ISecurityChallengeIdentityService>().Authenticate(request["username"], Guid.Parse(request["challenge"]), request["response"], tfa);
                            if (principal != null)
                                session = sessionService.Establish(principal, remoteEp, isOverride, purposeOfUse, scopes, request["ui_locales"]);
                            else
                                throw new SecurityException("Could not authenticate principal");
                            break;
                        }
                    case "password":
                        {
                            IPrincipal principal = null;
                            if (isOverride && identityService is IElevatableIdentityProviderService elevatedAuth)
                                principal = elevatedAuth.ElevatedAuthenticate(request["username"], request["password"], tfa, purposeOfUse, scopes);
                            else if (!String.IsNullOrEmpty(tfa))
                                principal = identityService.Authenticate(request["username"], request["password"], tfa);
                            else
                                principal = identityService.Authenticate(request["username"], request["password"]);

                            AuthenticationContext.Current = new AuthenticationContext(principal);

                            var lanugageCode = request["ui_locales"] ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

                            try
                            {
                                // TODO: Authenticate the device 
                                var userEntity = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>()?.GetUserEntity(principal.Identity);
                                if (userEntity != null)
                                    lanugageCode = userEntity?.LanguageCommunication?.FirstOrDefault(o => o.IsPreferred)?.LanguageCode ?? lanugageCode;
                            }
                            catch(Exception e) {
                                this.m_tracer.TraceWarning("Cannot set the language of session from user preferences - {0}", e);
                            } // Minor problem

                            if (principal != null)
                                session = sessionService.Establish(principal, remoteEp, isOverride, purposeOfUse, scopes, lanugageCode);
                            else
                                throw new SecurityException("Could not authenticate principal");

                        }
                        break;
                    case "refresh_token":
                        {
                            byte[] refreshTokenData = request["refresh_token"].ParseHexString(),
                                refreshToken = refreshTokenData.Take(16).ToArray(),
                                signature = refreshTokenData.Skip(16).ToArray();
                            if (!signatureService.Verify(refreshToken, signature))
                                throw new SecurityException("Refresh token signature mismatch");

                            // Get the local session               
                            session = sessionService.Extend(refreshToken);
                            break;
                        }
                    case "pin":
                        {
                            var pinAuthSvc = ApplicationServiceContext.Current.GetService<IPinAuthenticationService>();
                            IPrincipal principal = pinAuthSvc.Authenticate(request["username"], request["pin"].Select(o => Byte.Parse(o.ToString())).ToArray());
                            if (principal != null)
                                session = sessionService.Establish(principal, remoteEp, isOverride, purposeOfUse, scopes, request["ui_locales"]);
                            else
                                throw new SecurityException("Could not authenticate principal");
                            break;
                        }
                    case "client_credentials": // Someone is asking to use the device credentials ... Make sure they can login on their current service
                        {
                            var devAuthSvc = ApplicationContext.Current.GetService<IDeviceIdentityProviderService>();
                            var pep = ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>();

                            // Is the user allowed to use just the device credential?
                            pep.Demand(PermissionPolicyIdentifiers.LoginImpersonateApplication);

                            var appConfig = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SecurityConfigurationSection>();
                            var rmtPrincipal = devAuthSvc.Authenticate(appConfig.DeviceName, appConfig.DeviceSecret);
                            scopes = scopes ?? new String[] { "*" };
                            session = sessionService.Establish(rmtPrincipal, remoteEp, false, null, scopes.Union(new String[] { PermissionPolicyIdentifiers.LoginImpersonateApplication }).ToArray(), request["ui_locales"]);
                            break;
                        }
                }

               
                var retVal = new OAuthTokenResponse()
                {
                    // TODO: Sign the access token
                    AccessToken = $"{session.Id.ToHexString()}{signatureService.SignData(session.Id).ToHexString()}",
                    RefreshToken = $"{session.RefreshToken.ToHexString()}{signatureService.SignData(session.RefreshToken).ToHexString()}",
                    ExpiresIn = (int)DateTime.Now.Subtract(session.NotAfter.DateTime).TotalSeconds,
                    TokenType = "bearer",
                    IdToken = this.HydrateToken(session)
                };

                return retVal;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error establishing session for {0}: {1}", request["username"], e);
                RestOperationContext.Current.OutgoingResponse.StatusCode = 400;

                // Was the original exception thrown a remote OAUTH response?
                if (e.InnerException is RestClientException<OAuthTokenResponse> oauth)
                    return oauth.Result;
                else
                    return new OAuthTokenResponse()
                    {
                        Error = "invalid_grant",
                        ErrorDescription = e.Message
                    };
            }
        }

        /// <summary>
        /// Hydrates an identity token
        /// </summary>
        private string HydrateToken(ISession session)
        {
            IDictionary<String, Object> idTokenClaims = new Dictionary<String, Object>();
            Dictionary<String, String> claimMap = new Dictionary<string, string>() {
                { SanteDBClaimTypes.DefaultNameClaimType , "unique_name" },
                { SanteDBClaimTypes.DefaultRoleClaimType, "role"  },
                { SanteDBClaimTypes.Sid, "sub" },
                { SanteDBClaimTypes.AuthenticationMethod , "authmethod" },
                { SanteDBClaimTypes.Expiration, "exp" },
                { SanteDBClaimTypes.AuthenticationInstant , "nbf" },
                { SanteDBClaimTypes.Email , "email" },
                { SanteDBClaimTypes.Telephone , "tel" }
            };

            foreach (var clm in session.Claims.GroupBy(o => o.Type))
            {
                if (!claimMap.TryGetValue(clm.Key, out string jwtOption))
                    jwtOption = clm.Key;
                idTokenClaims.Add(jwtOption, clm.Count() == 1 ? (object)clm.First().Value : clm.Select(o => o.Value).ToArray());
            }

            var payload = UrlEncodeUtil(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(idTokenClaims))));
            var header = UrlEncodeUtil(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
            {
                typ = "JWT",
                alg = "HS256"
            }))));

            var signingService = ApplicationServiceContext.Current.GetService<IDataSigningService>();
            var hash = UrlEncodeUtil(Convert.ToBase64String(signingService.SignData(Encoding.UTF8.GetBytes($"{header}.{payload}"))));
            return $"{header}.{payload}.{hash}";

        }

        /// <summary>
        /// Utility for JWT signature
        /// </summary>
        private string UrlEncodeUtil(string source) => source.Replace('+', '-')
                  .Replace('/', '_')
                  .Replace("=", "");

        /// <summary>
        /// Get the specified session information
        /// </summary>
        public SessionInfo GetSession()
        {
            this.ThrowIfNotRunning();

            if (RestOperationContext.Current.Data.TryGetValue(AgsAuthorizationServiceBehavior.SessionPropertyName, out object session))
                return new SessionInfo(session as ISession);
            return null;
        }

        /// <summary>
        /// Perform a pre-check
        /// </summary>
        public void AclPreCheck(string policyId)
        {
            this.ThrowIfNotRunning();

            ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(policyId);
        }
    }
}
