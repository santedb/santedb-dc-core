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
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Xamarin.Exceptions;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Xamarin.Security
{
    /// <summary>
    /// Represents an OAuthIdentity provider
    /// </summary>
    public class OAuthIdentityProvider : IIdentityProviderService
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
        /// Authenticate the user
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        public System.Security.Principal.IPrincipal Authenticate(string userName, string password)
        {
            return this.Authenticate(new GenericPrincipal(new GenericIdentity(userName), null), password);
        }

        /// <summary>
        /// Perform authentication with specified password
        /// </summary>
        public System.Security.Principal.IPrincipal Authenticate(System.Security.Principal.IPrincipal principal, string password)
        {
            return this.Authenticate(principal, password, null);
        }

        /// <summary>
        /// Authenticate the user
        /// </summary>
        /// <param name="principal">Principal.</param>
        /// <param name="password">Password.</param>
        public System.Security.Principal.IPrincipal Authenticate(System.Security.Principal.IPrincipal principal, string password, String tfaSecret)
        {

            AuthenticatingEventArgs e = new AuthenticatingEventArgs(principal.Identity.Name) { Principal = principal };
            this.Authenticating?.Invoke(this, e);
            if (e.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event ordered cancel of auth {0}", principal);
                return e.Principal;
            }

            var localIdp = ApplicationContext.Current.GetService<IOfflineIdentityProviderService>();

            // Get the scope being requested
            String scope = "*";
            if (principal is IOfflinePrincipal && password == null)
                return localIdp.ReAuthenticate(principal);

            // Authenticate
            IPrincipal retVal = null;

            try
            {
                using (IRestClient restClient = ApplicationContext.Current.GetRestClient("acs"))
                {

                    try
                    {
                        // TODO: Add claims for elevation!
                        var scopeClaim = (principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.SanteDBScopeClaim)?.Value;
                        var overrideClaim = (principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.SanteDBOverrideClaim)?.Value;
                        var purposeOfUseClaim = (principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.XspaPurposeOfUseClaim)?.Value;

                        if (!String.IsNullOrEmpty(scopeClaim))
                            scope = scopeClaim;

                        // Create grant information
                        OAuthTokenRequest request = null;
                        if (!String.IsNullOrEmpty(password))
                            request = new OAuthTokenRequest(principal.Identity.Name, password, scope);
                        else if (principal is TokenClaimsPrincipal)
                            request = new OAuthTokenRequest(principal as TokenClaimsPrincipal, scope);
                        else
                            request = new OAuthTokenRequest(principal.Identity.Name, null, scope);

                        // Set credentials
                        if (ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DomainAuthentication == DomainClientAuthentication.Basic)
                            restClient.Credentials = new OAuthTokenServiceCredentials(principal);
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
                                if (overrideClaim == "true")
                                {
                                    p.AdditionalHeaders.Add(HeaderTypes.HttpClaims, Convert.ToBase64String(Encoding.UTF8.GetBytes(
                                            $"{SanteDBClaimTypes.SanteDBOverrideClaim}=true;{SanteDBClaimTypes.XspaPurposeOfUseClaim}={purposeOfUseClaim}"
                                        )));
                                }

                                if (!String.IsNullOrEmpty(tfaSecret))
                                    p.AdditionalHeaders.Add(HeaderTypes.HttpTfaSecret, tfaSecret);
                            };

                            // Invoke
                            if (ApplicationServiceContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                            {
                                restClient.Description.Endpoint[0].Timeout = 5000;
                                restClient.Invoke<Object, Object>("PING", "/", null, null);

                                if (principal.Identity.Name == ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceName)
                                    restClient.Description.Endpoint[0].Timeout = restClient.Description.Endpoint[0].Timeout * 2;
                                else
                                    restClient.Description.Endpoint[0].Timeout = (int)(restClient.Description.Endpoint[0].Timeout * 0.6666f);

                                OAuthTokenResponse response = restClient.Post<OAuthTokenRequest, OAuthTokenResponse>("oauth2_token", "application/x-www-form-urlencoded", request);
                                retVal = new TokenClaimsPrincipal(response.AccessToken, response.IdToken ?? response.AccessToken, response.TokenType, response.RefreshToken);
                            }
                            else
                            {
                                this.m_tracer.TraceWarning("Network unavailable, trying local");
                                try
                                {
                                    if (localIdp == null)
                                        throw new SecurityException(Strings.err_offline_no_local_available);

                                    if (!String.IsNullOrEmpty(password))
                                        retVal = localIdp.Authenticate(principal.Identity.Name, password);
                                    else
                                        retVal = localIdp.ReAuthenticate(principal);
                                }
                                catch (Exception ex2)
                                {
                                    this.m_tracer.TraceError("Error falling back to local IDP: {0}", ex2);
                                    throw new SecurityException(String.Format(Strings.err_offline_use_cache_creds, ex2.Message), ex2);
                                }
                            }
                            this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(principal.Identity.Name, retVal, true));

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
                        catch (WebException ex) // Raw level web exception
                        {
                            // Not network related, but a protocol level error
                            if (ex.Status == WebExceptionStatus.ProtocolError)
                                throw;

                            this.m_tracer.TraceWarning("Original OAuth2 request failed trying local - Original Exception : {0}", ex);
                            try
                            {
                                if (localIdp == null)
                                    throw new SecurityException(Strings.err_offline_no_local_available);
                                if (!String.IsNullOrEmpty(password))
                                    retVal = localIdp.Authenticate(principal.Identity.Name, password);
                                else
                                    retVal = localIdp.ReAuthenticate(principal);
                            }
                            catch (Exception ex2)
                            {
                                this.m_tracer.TraceError("Error falling back to local IDP: {0}", ex2);
                                throw new SecurityException(String.Format(Strings.err_offline_use_cache_creds, ex2.Message), ex2);
                            }
                        }
                        catch (SecurityException ex)
                        {
                            this.m_tracer.TraceError("Server was contacted however the token is invalid: {0}", ex.Message);
                            throw;
                        }
                        catch (Exception ex) // fallback to local
                        {
                            try
                            {
                                this.m_tracer.TraceWarning("Original OAuth2 request failed trying local - Original Exception : {0}", ex);

                                if (localIdp == null)
                                    throw new SecurityException(Strings.err_offline_no_local_available);

                                if (!String.IsNullOrEmpty(password))
                                    retVal = localIdp.Authenticate(principal.Identity.Name, password);
                                else
                                    retVal = localIdp.ReAuthenticate(principal);
                            }
                            catch (Exception ex2)
                            {
                                this.m_tracer.TraceError("Error falling back to local IDP: {0}", ex2);

                                throw new SecurityException(String.Format(Strings.err_offline_use_cache_creds, ex2.Message), ex2);
                            }
                        }


                        // We have a match! Lets make sure we cache this data
                        // TODO: Clean this up
                        try
                        {
                            if (!(retVal is IOfflinePrincipal))
                                ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(o => this.SynchronizeSecurity(password, o as IPrincipal), retVal);
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                this.m_tracer.TraceWarning("Failed to fetch remote security parameters - {0}", ex.Message);

                                if (localIdp == null)
                                    throw new SecurityException(Strings.err_offline_no_local_available);

                                if (!String.IsNullOrEmpty(password))
                                    retVal = localIdp.Authenticate(principal.Identity.Name, password);
                                else
                                    retVal = localIdp.ReAuthenticate(principal);
                            }
                            catch (Exception ex2)
                            {
                                this.m_tracer.TraceError("Error falling back to local IDP: {0}", ex2);
                                throw new SecurityException(String.Format(Strings.err_offline_use_cache_creds, ex2.Message));
                            }
                        }
                    }
                    catch (SecurityTokenException ex)
                    {
                        this.m_tracer.TraceError("TOKEN exception: {0}", ex.Message);
                        throw new SecurityException(
                            String.Format("err_token_{0}", ex.Type),
                            ex
                        );
                    }
                    catch (SecurityException ex)
                    {
                        this.m_tracer.TraceError("Security exception: {0}", ex.Message);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceError("Generic exception: {0}", ex);
                        throw new SecurityException(
                            Strings.err_authentication_exception,
                            ex);
                    }
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("OAUTH Error: {0}", ex.ToString());
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(principal.Identity.Name, retVal, false));
                throw;
            }

            return retVal;
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
                            XamarinApplicationContext.Current.ConfigurationPersister.IsConfigured)
            {
                AuthenticationContext.Current = new AuthenticationContext(principal);
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
                        var amiPolicies = amiPip.GetActivePolicies(new SecurityRole() { Name = itm.Value }).ToArray();
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

                var localUser = XamarinApplicationContext.Current.ConfigurationPersister.IsConfigured ? localIdp.GetIdentity(principal.Identity.Name) : null;

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
            return this.Authenticate(new GenericPrincipal(new GenericIdentity(userName), null), password, tfaSecret);
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
                var offlineIdService = ApplicationContext.Current.GetService<IOfflineRoleProviderService>();
                if (offlineIdService?.IsUserInRole(userName, "LOCAL_USERS") == true) // User is a local user, so we only change password on local
                {
                    ApplicationContext.Current.GetService<IOfflineIdentityProviderService>().ChangePassword(userName, newPassword, principal);
                }
                else
                {
                    using (AmiServiceClient client = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami")))
                    {
                        client.Client.Accept = "application/xml";

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
                        var localIdp = ApplicationContext.Current.GetService<IOfflineIdentityProviderService>();

                        // Change locally
                        localIdp?.ChangePassword(userName, newPassword, principal);

                        // Audit - Local IDP has alerted this already
                        AuditUtil.AuditSecurityAttributeAction(new object[] { user }, true, new string[] { "password" });
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
            return this.Authenticate(principal, null);
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

        public void AddClaim(string userName, IClaim claim, IPrincipal prinicpal,TimeSpan? expiry = null)
        {
            throw new NotSupportedException();
        }

        public void RemoveClaim(string userName, string claimType, IPrincipal prinicpal)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}

