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
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Exceptions;
using System;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents the OAUTH device identity provider
    /// </summary>
    public class OAuthDeviceIdentityProvider : IDeviceIdentityProviderService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "OAUTH 2.0 Device Identity Provider";

        // Log tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(OAuthIdentityProvider));

        // Lock object
        private Object m_lockObject = new object();

        /// <summary>
        /// Device is authenticated
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// Device is authenticating
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;


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
        /// Authenticate the specified device
        /// </summary>
        public IPrincipal Authenticate(string deviceId, string deviceSecret, AuthenticationMethod authMethod = AuthenticationMethod.Any)
        {
            AuthenticatingEventArgs e = new AuthenticatingEventArgs(deviceId);
            this.Authenticating?.Invoke(this, e);
            if (e.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event ordered cancel of auth {0}", deviceId);
                return e.Principal;
            }

            // Get the scope being requested
            String scope = "*";

            // Authenticate
            IPrincipal retVal = null;

            using (IRestClient restClient = ApplicationContext.Current.GetRestClient("acs"))
            {

                // Create grant information
                OAuthTokenRequest request = OAuthTokenRequest.CreateClientCredentialRequest(ApplicationContext.Current.Application.Name, ApplicationContext.Current.Application.ApplicationSecret, "*");

                try
                {

                    restClient.Requesting += (o, p) =>
                    {
                        // Add device credential
                        p.AdditionalHeaders.Add("X-Device-Authorization", $"BASIC {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{deviceId}:{deviceSecret}"))}");
                    };

                    // Invoke
                    if (authMethod.HasFlag(AuthenticationMethod.Online) &&
                        ApplicationServiceContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable &&
                        ApplicationServiceContext.Current.GetService<IAdministrationIntegrationService>()?.IsAvailable() != false) // Network may be on but internet is not available
                    {
                        restClient.Description.Endpoint[0].Timeout = (int)(restClient.Description.Endpoint[0].Timeout * 0.333f);

                        var configuration = this.GetConfigurationInfo();
                        if (configuration == null)
                        {
                            OAuthTokenResponse response = restClient.Post<OAuthTokenRequest, OAuthTokenResponse>("oauth2_token", "application/x-www-form-urlencoded", request);
                            retVal = new TokenClaimsPrincipal(response.AccessToken, response.IdToken ?? response.AccessToken, response.TokenType, response.RefreshToken, configuration);
                        }
                        else
                        {
                            OAuthTokenResponse response = restClient.Post<OAuthTokenRequest, OAuthTokenResponse>(configuration.TokenEndpoint, "application/x-www-form-urlencoded", request);
                            retVal = new TokenClaimsPrincipal(response.AccessToken, response.IdToken ?? response.AccessToken, response.TokenType, response.RefreshToken, configuration);
                        }

                        // HACK: Set preferred sid to device SID
                        var cprincipal = retVal as IClaimsPrincipal;

                        var devId = cprincipal.FindFirst(SanteDBClaimTypes.SanteDBDeviceIdentifierClaim);
                        if (devId != null)
                        {
                            cprincipal.Identities.First().RemoveClaim(cprincipal.FindFirst(SanteDBClaimTypes.Sid));
                            cprincipal.Identities.First().AddClaim(new SanteDBClaim(SanteDBClaimTypes.Sid, devId.Value));
                        }

                        // Synchronize the security devices
                        try
                        {
                            this.SynchronizeSecurity(deviceSecret, deviceId, retVal);
                        }
                        catch(Exception e2 )
                        {
                            this.m_tracer.TraceError("Error synchronizing the local device security: {0}", e2);
                        }
                        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(deviceId, retVal, true) { Principal = retVal });
                    }
                    else if (authMethod.HasFlag(AuthenticationMethod.Local))
                    {
                        // Is this another device authenticating against me?
                        if (deviceId != ApplicationContext.Current.Device.Name)
                        {
                            return ApplicationContext.Current.GetService<IOfflineDeviceIdentityProviderService>().Authenticate(deviceId, deviceSecret);
                        }
                        else
                        {
                            this.m_tracer.TraceWarning("Network unavailable, skipping authentication - Authenticating as SELF");
                            throw new SecurityException(Strings.err_network_securityNotAvailable);
                        }
                    }
                    else
                        throw new InvalidOperationException("Cannot determine authentication method");
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
                    this.m_tracer.TraceError("Server was contacted however the token is invalid: {0}", ex.Message);
                    throw;
                }
                catch (RestClientException<OAuthTokenResponse> ex)
                {
                    this.m_tracer.TraceError("REST client exception: {0}", ex.Message);

                    // Not network related, but a protocol level error
                    if (authMethod.HasFlag(AuthenticationMethod.Local) && deviceId != ApplicationContext.Current.Device.Name)
                    {
                        this.m_tracer.TraceWarning("Network unavailable falling back to local");
                        return ApplicationContext.Current.GetService<IOfflineDeviceIdentityProviderService>().Authenticate(deviceId, deviceSecret);
                    }
                    else
                    {
                        this.m_tracer.TraceWarning("Original OAuth2 request failed: {0}", ex.Message);
                        throw new SecurityException(Strings.err_authentication_exception, ex);
                    }
                }
                catch (Exception ex) // Raw level web exception
                {
                    // Not network related, but a protocol level error
                    if (authMethod.HasFlag(AuthenticationMethod.Local) && deviceId != ApplicationContext.Current.Device.Name)
                    {
                        this.m_tracer.TraceWarning("Network unavailable falling back to local");
                        return ApplicationContext.Current.GetService<IOfflineDeviceIdentityProviderService>().Authenticate(deviceId, deviceSecret);
                    }
                    else
                    {
                        this.m_tracer.TraceWarning("Original OAuth2 request failed: {0}", ex.Message);
                        throw new SecurityException(Strings.err_authentication_exception, ex);
                    }
                }

                return retVal;
            }
        }

        /// <summary>
        /// Synchronize security locally 
        /// </summary>
        private void SynchronizeSecurity(string deviceSecret, string deviceName, IPrincipal principal)
        {
            // Create a security user and ensure they exist!
            var localPip = ApplicationContext.Current.GetService<IOfflinePolicyInformationService>();
            var localIdp = ApplicationContext.Current.GetService<IOfflineDeviceIdentityProviderService>();
            var sdPersistence = ApplicationContext.Current.GetService<IDataPersistenceService<SecurityDevice>>();

            if (localIdp != null &&
                !String.IsNullOrEmpty(deviceSecret) && principal is IClaimsPrincipal &&
                            ApplicationContext.Current.ConfigurationPersister.IsConfigured)
            {
                AuthenticationContext.Current = new AuthenticationContext(principal);
                IClaimsPrincipal cprincipal = principal as IClaimsPrincipal;
                var amiPip = new AmiPolicyInformationService();

                // Local device
                int tr = 0;

                IIdentity localDeviceIdentity = null;
                lock (this.m_lockObject)
                {
                    try
                    {
                        localDeviceIdentity = localIdp.GetIdentity(deviceName);
                        Guid sid = Guid.Parse(cprincipal.FindFirst(SanteDBClaimTypes.SanteDBDeviceIdentifierClaim).Value);
                        if (localDeviceIdentity == null)
                            localDeviceIdentity = localIdp.CreateIdentity(sid, deviceName, deviceSecret, AuthenticationContext.SystemPrincipal);
                        else
                            localIdp.ChangeSecret(deviceName, deviceSecret, AuthenticationContext.SystemPrincipal);
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceWarning("Insertion of local cache credential failed: {0}", ex);
                    }

                    // Ensure policies exist from the claim
                    foreach (var itm in cprincipal.Claims.Where(o => o.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim))
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

                    localPip.AddPolicies(localDeviceIdentity, PolicyGrantType.Grant, AuthenticationContext.SystemPrincipal, cprincipal.Claims.Where(o => o.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(o => o.Value).ToArray());
                }
            }
        }

        /// <summary>
        /// Get the identity of the specified object
        /// </summary>
        public IIdentity GetIdentity(string name)
        {
            var offlineIdService = ApplicationServiceContext.Current.GetService<IOfflineDeviceIdentityProviderService>();
            if (offlineIdService == null)
                throw new InvalidOperationException("Offline identity provider service is not available");
            return offlineIdService.GetIdentity(name);
        }

        /// <summary>
        /// Set the lockout of the specified objet
        /// </summary>
        public void SetLockout(string name, bool lockoutState, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Change the secret of the device
        /// </summary>
        public void ChangeSecret(string name, string deviceSecret, IPrincipal systemPrincipal)
        {
            throw new NotSupportedException();

        }
    }
}
