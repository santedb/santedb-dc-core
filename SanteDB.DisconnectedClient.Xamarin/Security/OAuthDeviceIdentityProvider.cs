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
 * Date: 2018-11-7
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Xamarin.Exceptions;
using System;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Xamarin.Security
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
        /// Authenticate the specified device
        /// </summary>
        public IPrincipal Authenticate(string deviceId, string deviceSecret)
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
                        p.AdditionalHeaders.Add("X-Device-Authorization", $"BASIC {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApplicationContext.Current.Device.Name}:{ApplicationContext.Current.Device.DeviceSecret}"))}");
                    };

                    // Invoke
                    if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    {
                        restClient.Description.Endpoint[0].Timeout = (int)(restClient.Description.Endpoint[0].Timeout * 0.6666f);
                        OAuthTokenResponse response = restClient.Post<OAuthTokenRequest, OAuthTokenResponse>("oauth2_token", "application/x-www-urlform-encoded", request);
                        retVal = new TokenClaimsPrincipal(response.AccessToken, response.IdToken ?? response.AccessToken, response.TokenType, response.RefreshToken);
                        this.SynchronizeSecurity(deviceSecret, retVal);
                        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(deviceId, retVal, true) { Principal = retVal });
                    }
                    else
                    {
                        // Is this another device authenticating against me?
                        if (deviceId != ApplicationContext.Current.Device.Name)
                        {
                            this.m_tracer.TraceWarning("Network unavailable falling back to local");
                            return ApplicationContext.Current.GetService<IOfflineDeviceIdentityProviderService>().Authenticate(deviceId, deviceSecret);
                        }
                        else
                        {
                            this.m_tracer.TraceWarning("Network unavailable, skipping authentication");
                            throw new SecurityException(Strings.err_network_securityNotAvailable);
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
                    this.m_tracer.TraceError("Server was contacted however the token is invalid: {0}", ex.Message);
                    throw;
                }
                catch (RestClientException<OAuthTokenResponse> ex)
                {
                    this.m_tracer.TraceError("REST client exception: {0}", ex.Message);
                    var se = new SecurityException(
                        String.Format("err_oauth2_{0}", ex.Result.Error),
                        ex
                    );
                    se.Data.Add("detail", ex.Result);
                    throw new SecurityException(Strings.err_authentication_exception, ex);
                }
                catch (Exception ex) // Raw level web exception
                {
                    // Not network related, but a protocol level error
                    if (deviceId != ApplicationContext.Current.Device.Name)
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
        private void SynchronizeSecurity(string deviceSecret, IPrincipal principal)
        {
            // Create a security user and ensure they exist!
            var localPip = ApplicationContext.Current.GetService<IOfflinePolicyInformationService>();
            var localIdp = ApplicationContext.Current.GetService<IOfflineDeviceIdentityProviderService>();
            var sdPersistence = ApplicationContext.Current.GetService<IDataPersistenceService<SecurityDevice>>();

            if (!String.IsNullOrEmpty(deviceSecret) && principal is ClaimsPrincipal &&
                            XamarinApplicationContext.Current.ConfigurationPersister.IsConfigured)
            {
                ClaimsPrincipal cprincipal = principal as ClaimsPrincipal;
                var amiPip = new AmiPolicyInformationService(cprincipal);

                // Local device
                int tr = 0;

                IIdentity localDeviceIdentity = null;
                lock (this.m_lockObject)
                    try
                    {
                        localDeviceIdentity = localIdp.GetIdentity(principal.Identity.Name);
                        Guid sid = Guid.Parse(cprincipal.FindClaim(ClaimTypes.SanteDBDeviceIdentifierClaim).Value);
                        if (localDeviceIdentity == null)
                            localDeviceIdentity = localIdp.CreateIdentity(sid, principal.Identity.Name, deviceSecret, AuthenticationContext.SystemPrincipal);
                        else
                            localIdp.ChangeSecret(principal.Identity.Name, deviceSecret, AuthenticationContext.SystemPrincipal);
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceWarning("Insertion of local cache credential failed: {0}", ex);
                    }

                // Ensure policies exist from the claim
                foreach (var itm in cprincipal.Claims.Where(o => o.Type == ClaimTypes.SanteDBGrantedPolicyClaim))
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

                // Re-lock to add policies
                lock(this.m_lockObject)
                    localPip.AddPolicies(localDeviceIdentity, PolicyGrantType.Grant, AuthenticationContext.SystemPrincipal, cprincipal.Claims.Where(o => o.Type == ClaimTypes.SanteDBGrantedPolicyClaim).Select(o => o.Value).ToArray());

            }
        }
    }
}
