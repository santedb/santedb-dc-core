/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents an OAuthIdentity provider
    /// </summary>
    public class OAuthIdentityProvider : IElevatableIdentityProviderService, ISecurityChallengeIdentityService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "OAuth 2.0 Identity Provider Service";

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(OAuthIdentityProvider));

        #region IIdentityProviderService implementation

        /// <summary>
        /// Occurs when authenticating.
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        /// <summary>
        /// Occurs when authenticated.
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// An override has been requested
        /// </summary>
        public event EventHandler<SecurityOverrideEventArgs> OverrideRequested;

        /// <summary>
        /// Authenticate the user
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        public System.Security.Principal.IPrincipal Authenticate(string userName, string password)
        {
            return this.Authenticate(userName, password, null);
        }

        /// <summary>
        /// Gets the configuration information for the OpenID service
        /// </summary>
        private OpenIdConfigurationInfo GetConfigurationInfo()
        {
            try
            {
                using (IRestClient restClient = ApplicationContext.Current.GetRestClient("acs"))
                {
                    restClient.Description.Endpoint[0].Timeout = 2000;
                    return restClient.Get<OpenIdConfigurationInfo>(".well-known/openid-configuration");
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error fetching OpenID configuration settings: {0}", e);
                return null;
            }
        }

        /// <summary>
        /// Synchronize the security settings
        /// </summary>
        /// <param name="password"></param>
        /// <param name="principal"></param>
        private void SynchronizeSecurity(string password, IPrincipal principal)
        {
            // Create a security user and ensure they exist!
            var localRp = ApplicationContext.Current.GetService<IOfflineRoleProviderService>();
            var localPip = ApplicationContext.Current.GetService<IOfflinePolicyInformationService>();
            var localIdp = ApplicationContext.Current.GetService<IOfflineIdentityProviderService>();

            if (localRp == null || localPip == null || localIdp == null)
                return;

            if (!String.IsNullOrEmpty(password) && principal is IClaimsPrincipal &&
                            ApplicationContext.Current.ConfigurationPersister.IsConfigured)
            {
                using (AuthenticationContext.EnterContext(principal))
                {
                    IClaimsPrincipal cprincipal = principal as IClaimsPrincipal;
                    var amiPip = new AmiPolicyInformationService();

                    // We want to impersonate SYSTEM
                    //AndroidApplicationContext.Current.SetPrincipal(cprincipal);

                    // Ensure policies exist from the claim
                    foreach (var itm in cprincipal.Claims.Where(o => o.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).ToArray())
                    {
                        if (localPip.GetPolicy(itm.Value) == null)
                        {
                            try
                            {
                                var policy = amiPip.GetPolicy(itm.Value);
                                localPip.CreatePolicy(policy, AuthenticationContext.SystemPrincipal);
                            }
                            catch (Exception e)
                            {
                                this.m_tracer.TraceWarning("Cannot update local policy information : {0}", e.Message);
                            }
                        }
                    }

                    // Ensure roles exist from the claim
                    var localRoles = localRp.GetAllRoles();
                    foreach (var itm in cprincipal.Claims.Where(o => o.Type == SanteDBClaimTypes.DefaultRoleClaimType).ToArray())
                    {
                        // Ensure policy exists
                        try
                        {
                            var amiPolicies = amiPip.GetPolicies(new SecurityRole() { Name = itm.Value }).ToArray();
                            foreach (var pol in amiPolicies)
                                if (localPip.GetPolicy(pol.Policy.Oid) == null)
                                {
                                    var policy = amiPip.GetPolicy(pol.Policy.Oid);
                                    localPip.CreatePolicy(policy, AuthenticationContext.SystemPrincipal);
                                }

                            // Local role doesn't exist
                            if (!localRoles.Contains(itm.Value))
                            {
                                localRp.CreateRole(itm.Value, AuthenticationContext.SystemPrincipal);
                            }
                            localRp.AddPoliciesToRoles(amiPolicies, new String[] { itm.Value }, AuthenticationContext.SystemPrincipal);
                        }
                        catch (Exception e)
                        {
                            this.m_tracer.TraceWarning("Could not fetch / refresh policies: {0}", e.Message);
                        }
                    }

                    var localUser = ApplicationContext.Current.ConfigurationPersister.IsConfigured ? localIdp.GetIdentity(principal.Identity.Name) : null;

                    try
                    {
                        Guid sid = Guid.Parse(cprincipal.FindFirst(SanteDBClaimTypes.Sid).Value);
                        if (localUser == null)
                        {
                            localIdp.CreateIdentity(sid, principal.Identity.Name, password, AuthenticationContext.SystemPrincipal);
                        }
                        else
                        {
                            localIdp.ChangePassword(principal.Identity.Name, password, AuthenticationContext.SystemPrincipal);
                        }

                        // Copy security attributes
                        var localSu = ApplicationContext.Current.GetService<IDataPersistenceService<SecurityUser>>().Get(sid, null, true, AuthenticationContext.SystemPrincipal);
                        localSu.Email = cprincipal.FindFirst(SanteDBClaimTypes.Email)?.Value;
                        localSu.PhoneNumber = cprincipal.FindFirst(SanteDBClaimTypes.Telephone)?.Value;
                        localSu.LastLoginTime = DateTime.Now;
                        ApplicationContext.Current.GetService<IDataPersistenceService<SecurityUser>>().Update(localSu, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);

                        // Add user to roles
                        // TODO: Remove users from specified roles?
                        localRp.AddUsersToRoles(new String[] { principal.Identity.Name }, cprincipal.Claims.Where(o => o.Type == SanteDBClaimTypes.DefaultRoleClaimType).Select(o => o.Value).ToArray(), AuthenticationContext.SystemPrincipal);
                        // Unlock the account
                        localIdp.SetLockout(principal.Identity.Name, false, principal);
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceWarning("Insertion of local cache credential failed: {0}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the specified identity
        /// </summary>
        public System.Security.Principal.IIdentity GetIdentity(string userName)
        {
            // Get identity from the local synchronized provider
            return new GenericIdentity(userName, "OAUTH");
        }

        /// <summary>
        /// Gets the specified identity
        /// </summary>
        public System.Security.Principal.IIdentity GetIdentity(Guid uuid)
        {
            // Get identity from the local synchronized provider
            return ApplicationServiceContext.Current.GetService<IOfflineIdentityProviderService>().GetIdentity(uuid);
        }

        /// <summary>
        /// Authenticates the specified user
        /// </summary>
		public System.Security.Principal.IPrincipal Authenticate(string userName, string password, string tfaSecret)
        {
            return this.DoAuthenticationInternal(userName, password, tfaSecret: tfaSecret);
        }

        /// <summary>
        /// Do internal authentication
        /// </summary>
        private IPrincipal DoAuthenticationInternal(String userName = null, String password = null, string tfaSecret = null, bool isOverride = false, TokenClaimsPrincipal refreshPrincipal = null, string purposeOfUse = null, string[] policies = null)
        {
            AuthenticatingEventArgs e = new AuthenticatingEventArgs(userName);
            this.Authenticating?.Invoke(this, e);
            if (e.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event ordered cancel of auth {0}", userName);
                return e.Principal;
            }

            var localIdp = ApplicationContext.Current.GetService<IOfflineIdentityProviderService>();

            IPrincipal retVal = null;

            // Authenticate
            try
            {
                // Is the user a LOCAL_USER only?
                if (localIdp?.IsLocalUser(userName) == true)
                    retVal = localIdp.Authenticate(userName, password, tfaSecret);
                else using (IRestClient restClient = ApplicationContext.Current.GetRestClient("acs"))
                    {
                        // Construct oauth req
                        OAuthTokenRequest request = null;
                        if (refreshPrincipal != null)
                            request = new OAuthTokenRequest(refreshPrincipal, "*");
                        else if (!String.IsNullOrEmpty(password))
                            request = new OAuthTokenRequest(userName, password, "*");
                        else
                            request = new OAuthTokenRequest(userName, null, "*");

                        // Explicit policies
                        if (policies != null)
                            request.Scope = String.Join(" ", policies);

                        // Set credentials for oauth req
                        if (ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DomainAuthentication == DomainClientAuthentication.Basic)
                            restClient.Credentials = new OAuthTokenServiceCredentials(null);
                        else
                        {
                            request.ClientId = ApplicationContext.Current.Application.Name;
                            request.ClientSecret = ApplicationContext.Current.Application.ApplicationSecret;
                        }

                        restClient.Requesting += (o, p) =>
                        {
                            if (!String.IsNullOrEmpty(tfaSecret))
                                p.AdditionalHeaders.Add(HeaderTypes.HttpTfaSecret, tfaSecret);
                            if (isOverride)
                                p.AdditionalHeaders.Add(HeaderTypes.HttpClaims, Convert.ToBase64String(Encoding.UTF8.GetBytes(
                                    $"{SanteDBClaimTypes.PurposeOfUse}={purposeOfUse};{SanteDBClaimTypes.SanteDBOverrideClaim}=true"
                                    )));
                            // Add device credential
                            if (!String.IsNullOrEmpty(ApplicationContext.Current.Device.DeviceSecret))
                                p.AdditionalHeaders.Add(HeaderTypes.HttpDeviceAuthentication, $"BASIC {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApplicationContext.Current.Device.Name}:{ApplicationContext.Current.Device.DeviceSecret}"))}");
                        };

                        if (ApplicationServiceContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                        {
                            // Try OAUTH server
                            try
                            {
                                restClient.Description.Endpoint[0].Timeout = 5000;
                                restClient.Invoke<Object, Object>("PING", "/", null, null);

                                if (userName == ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceName)
                                    restClient.Description.Endpoint[0].Timeout = restClient.Description.Endpoint[0].Timeout * 2;
                                else
                                    restClient.Description.Endpoint[0].Timeout = (int)(restClient.Description.Endpoint[0].Timeout * 0.6666f);

                                // GEt configuration
                                var configuration = this.GetConfigurationInfo();
                                if (configuration == null) // default action
                                {
                                    var oauthResponse = restClient.Post<OAuthTokenRequest, OAuthTokenResponse>("oauth2_token", "application/x-www-form-urlencoded", request);
                                    retVal = new TokenClaimsPrincipal(oauthResponse.AccessToken, oauthResponse.IdToken ?? oauthResponse.AccessToken, oauthResponse.TokenType, oauthResponse.RefreshToken, configuration);
                                }
                                else
                                {
                                    if (!configuration.GrantTypesSupported.Contains("password"))
                                        throw new InvalidOperationException("Password grants not supported by this provider");

                                    var oauthResponse = restClient.Post<OAuthTokenRequest, OAuthTokenResponse>(configuration.TokenEndpoint, "application/x-www-form-urlencoded", request);
                                    retVal = new TokenClaimsPrincipal(oauthResponse.AccessToken, oauthResponse.IdToken ?? oauthResponse.AccessToken, oauthResponse.TokenType, oauthResponse.RefreshToken, configuration);
                                }
                            }
                            catch (RestClientException<OAuthTokenResponse> ex) // there was an actual OAUTH problem
                            {
                                this.m_tracer.TraceError("REST client exception: {0}", ex.Message);
                                throw new SecurityException($"err_oauth_{ex.Result.Error}", ex);
                            }
                            catch (SecurityException ex)
                            {
                                this.m_tracer.TraceError("Server was contacted however the token is invalid: {0}", ex.Message);
                                throw;
                            }
                            catch (Exception ex) // All others, try local
                            {
                                this.m_tracer.TraceWarning("Original OAuth2 request failed trying local - Original Exception : {0}", ex);
                            }

                            if (retVal == null) // Some error occurred, use local
                            {
                                this.m_tracer.TraceWarning("Network unavailable, trying local");
                                try
                                {
                                    if (localIdp == null)
                                        throw new SecurityException(Strings.err_offline_no_local_available);

                                    retVal = localIdp.Authenticate(userName, password);
                                }
                                catch (Exception ex2)
                                {
                                    this.m_tracer.TraceError("Error falling back to local IDP: {0}", ex2);
                                    throw new SecurityException(String.Format(Strings.err_offline_use_cache_creds, ex2.Message), ex2);
                                }
                            }

                            // We have a match! Lets make sure we cache this data
                            // TODO: Clean this up
                            if (!(retVal is IOfflinePrincipal))
                                try
                                {
                                    ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(o => this.SynchronizeSecurity(password, o as IPrincipal), retVal);
                                }
                                catch (Exception e2)
                                {
                                    this.m_tracer.TraceError("An error occurred when inserting the local credential: {0}", e2);
                                }
                        }
                        else
                            retVal = localIdp?.Authenticate(userName, password);
                    }

                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, retVal, true));
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("OAUTH Error: {0}", ex.ToString());
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, retVal, false));
                throw new SecurityException($"Error establishing authentication session - {ex.Message}", ex);
            }

            return retVal;
        }

        /// <summary>
        /// Changes the users password.
        /// </summary>
        /// <param name="userName">The username of the user.</param>
        /// <param name="newPassword">The new password of the user.</param>
        /// <param name="principal">The authentication principal (the user that is changing the password).</param>
        public void ChangePassword(string userName, string newPassword, System.Security.Principal.IPrincipal principal)
        {
            try
            {
                // The principal must change their own password or must have the changepassword credential
                if (!userName.Equals(principal.Identity.Name, StringComparison.InvariantCultureIgnoreCase))
                    new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.ChangePassword).Demand();
                else if (!principal.Identity.IsAuthenticated)
                    throw new InvalidOperationException("Unauthenticated principal cannot change user password");

                // Get the user's identity
                var securityUserService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
                var localIdp = ApplicationContext.Current.GetService<IOfflineIdentityProviderService>();
                if (localIdp?.IsLocalUser(userName) == true) // User is a local user, so we only change password on local
                {
                    localIdp.ChangePassword(userName, newPassword, principal);
                }
                else
                {
                    using (AmiServiceClient client = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami")))
                    {
                        Guid userId = Guid.Empty;
                        if (principal.Identity.Name.ToLowerInvariant() == userName.ToLowerInvariant())
                        {
                            var subjectClaim = (principal as IClaimsPrincipal).FindFirst(SanteDBClaimTypes.Sid);
                            if (subjectClaim != null)
                                userId = Guid.Parse(subjectClaim.Value);
                        }

                        // User ID not found - lookup
                        if (userId == Guid.Empty)
                        {
                            // User service is null
                            var securityUser = securityUserService.GetUser(userName);
                            if (securityUser == null)
                            {
                                var tuser = client.GetUsers(o => o.UserName == userName).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault();
                                if (tuser == null)
                                    throw new ArgumentException(string.Format("User {0} not found", userName));
                                else
                                    userId = tuser.Entity.Key.Value;
                            }
                            else
                                userId = securityUser.Key.Value;
                        }

                        // Use the current configuration's credential provider
                        var user = new SecurityUserInfo(new SecurityUser()
                        {
                            Key = userId,
                            UserName = userName,
                            Password = newPassword
                        })
                        {
                            PasswordOnly = true
                        };

                        // Set the credentials
                        client.Client.Credentials = ApplicationContext.Current.Configuration.GetServiceDescription("ami").Binding.Security.CredentialProvider.GetCredentials(principal);

                        client.UpdateUser(userId, user);

                        // Change locally
                        var userInfo = localIdp?.GetIdentity(userName);
                        if (userInfo != null)
                            localIdp?.ChangePassword(userName, newPassword, principal);

                        // Audit - Local IDP has alerted this already
                        AuditUtil.AuditSecurityAttributeAction(new object[] { user.ToIdentifiedData() }, true, new string[] { "password" });
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error changing password for user {0} : {1}", userName, e);
                throw;
            }
        }

        /// <summary>
        /// Perform re-auth
        /// </summary>
        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal is TokenClaimsPrincipal tokenClaim)
                return this.DoAuthenticationInternal(refreshPrincipal: tokenClaim);
            else
                return ApplicationServiceContext.Current.GetService<IOfflineIdentityProviderService>().ReAuthenticate(principal);
        }

        /// <summary>
        /// Creates an identity
        /// </summary>
        public IIdentity CreateIdentity(string userName, string password, IPrincipal principal)
        {
            if (ApplicationContext.Current.ConfigurationManager.GetAppSetting("security.localUsers") == "true")
                return ApplicationContext.Current.GetService<IOfflineIdentityProviderService>().CreateIdentity(userName, password, principal);
            else
                throw new InvalidOperationException(Strings.err_local_users_prohibited);
        }

        /// <summary>
        /// Sets the user's lockout status
        /// </summary>
        public void SetLockout(string userName, bool locked, IPrincipal principal)
        {
            if (ApplicationContext.Current.ConfigurationManager.GetAppSetting("security.localUsers") == "true")
                ApplicationContext.Current.GetService<IOfflineIdentityProviderService>().SetLockout(userName, locked, principal);
            else
                throw new InvalidOperationException(Strings.err_local_users_prohibited);
        }

        /// <summary>
        /// Deletes the specified identity
        /// </summary>
        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            if (ApplicationContext.Current.ConfigurationManager.GetAppSetting("security.localUsers") == "true")
                ApplicationContext.Current.GetService<IOfflineIdentityProviderService>().DeleteIdentity(userName, principal);
            else
                throw new InvalidOperationException(Strings.err_local_users_prohibited);
        }

        public string GenerateTfaSecret(string userName)
        {
            throw new NotSupportedException();
        }

        public void AddClaim(string userName, IClaim claim, IPrincipal prinicpal, TimeSpan? expiry = null)
        {
            throw new NotSupportedException();
        }

        public void RemoveClaim(string userName, string claimType, IPrincipal prinicpal)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Challenge key authentication for password change only
        /// </summary>
        public IPrincipal Authenticate(string userName, Guid challengeKey, string response, string tfaSecret)
        {
            AuthenticatingEventArgs e = new AuthenticatingEventArgs(userName);
            this.Authenticating?.Invoke(this, e);
            if (e.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event ordered cancel of auth {0}", userName);
                return e.Principal;
            }

            var localIdp = ApplicationContext.Current.GetService<IOfflineIdentityProviderService>();
            IPrincipal retVal = null;

            // Get the scope being requested
            try
            {
                if (localIdp?.IsLocalUser(userName) == true)
                {
                    var localScIdp = ApplicationServiceContext.Current.GetService<IOfflineSecurityChallengeIdentityService>();
                    retVal = localScIdp.Authenticate(userName, challengeKey, response, tfaSecret);
                }
                else using (IRestClient restClient = ApplicationContext.Current.GetRestClient("acs"))
                    {
                        // Create grant information
                        OAuthTokenRequest request = new OAuthResetTokenRequest(userName, challengeKey.ToString(), response);

                        // Set credentials
                        if (ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DomainAuthentication == DomainClientAuthentication.Basic)
                            restClient.Credentials = new OAuthTokenServiceCredentials(null);
                        else
                        {
                            request.ClientId = ApplicationContext.Current.Application.Name;
                            request.ClientSecret = ApplicationContext.Current.Application.ApplicationSecret;
                        }

                        try
                        {
                            restClient.Requesting += (o, p) =>
                            {
                                // Add device credential
                                if (!String.IsNullOrEmpty(ApplicationContext.Current.Device.DeviceSecret))
                                    p.AdditionalHeaders.Add(HeaderTypes.HttpDeviceAuthentication, $"BASIC {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApplicationContext.Current.Device.Name}:{ApplicationContext.Current.Device.DeviceSecret}"))}");

                                if (!String.IsNullOrEmpty(tfaSecret))
                                    p.AdditionalHeaders.Add(HeaderTypes.HttpTfaSecret, tfaSecret);
                            };

                            // Invoke
                            if (ApplicationServiceContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                            {
                                restClient.Description.Endpoint[0].Timeout = 5000;
                                restClient.Invoke<Object, Object>("PING", "/", null, null);
                                restClient.Description.Endpoint[0].Timeout = (int)(restClient.Description.Endpoint[0].Timeout * 0.6666f);

                                // Swap out the endpoint and authenticate
                                var configuration = this.GetConfigurationInfo();
                                if (configuration == null) // default action
                                {
                                    var oauthResponse = restClient.Post<OAuthTokenRequest, OAuthTokenResponse>("oauth2_token", "application/x-www-form-urlencoded", request);
                                    retVal = new TokenClaimsPrincipal(oauthResponse.AccessToken, oauthResponse.IdToken ?? oauthResponse.AccessToken, oauthResponse.TokenType, oauthResponse.RefreshToken, configuration);
                                }
                                else
                                {
                                    if (!configuration.GrantTypesSupported.Contains("password"))
                                        throw new InvalidOperationException("Password grants not supported by this provider");

                                    var oauthResponse = restClient.Post<OAuthTokenRequest, OAuthTokenResponse>(configuration.TokenEndpoint, "application/x-www-form-urlencoded", request);
                                    retVal = new TokenClaimsPrincipal(oauthResponse.AccessToken, oauthResponse.IdToken ?? oauthResponse.AccessToken, oauthResponse.TokenType, oauthResponse.RefreshToken, configuration);
                                }

                                return retVal;
                            }
                            else
                            {
                                throw new InvalidOperationException("Cannot send reset credential while offline");
                            }
                        }
                        catch (RestClientException<OAuthTokenResponse> ex)
                        {
                            this.m_tracer.TraceError("REST client exception: {0}", ex.Message);
                            var se = new SecurityException(
                                String.Format("err_oauth2_{0}", ex.Result.Error),
                                ex
                            );
                            se.Data.Add("oauth_result", ex.Result);
                            throw se;
                        }
                        catch (SecurityException ex)
                        {
                            this.m_tracer.TraceError("Server was contacted however the token is invalid: {0}", ex.Message);
                            throw;
                        }
                        catch (Exception ex) // fallback to local
                        {
                            throw new SecurityException($"General authentication error occurred: {ex.Message}", ex);
                        }
                    }

                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, retVal, true));
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("OAUTH Error: {0}", ex.ToString());
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, null, false));
                throw;
            }

            return retVal;
        }

        /// <summary>
        /// Elevated authentication
        /// </summary>
        public IPrincipal ElevatedAuthenticate(string userName, string password, string tfaSecret, string purpose, params string[] policies)
        {
            return this.DoAuthenticationInternal(userName: userName, password: password, tfaSecret: tfaSecret, isOverride: true, purposeOfUse: purpose, policies: policies);
        }

        #endregion IIdentityProviderService implementation
    }
}