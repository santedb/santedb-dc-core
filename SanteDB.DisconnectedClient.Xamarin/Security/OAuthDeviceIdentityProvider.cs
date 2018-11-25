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
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Xamarin.Exceptions;
using System;
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
        // Log tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(OAuthIdentityProvider));

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
            AuthenticatingEventArgs e = new AuthenticatingEventArgs(deviceId, deviceSecret);
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
                        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(deviceId, deviceSecret, true) { Principal = retVal });
                    }
                    else
                    {
                        this.m_tracer.TraceWarning("Network unavailable, skipping authentication");
                        throw new SecurityException(Strings.err_network_securityNotAvailable);
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
                    this.m_tracer.TraceWarning("Original OAuth2 request failed: {0}", ex.Message);
                    throw new SecurityException(Strings.err_authentication_exception, ex);
                }

                return retVal;
            }
        }

    }
}
