/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using SanteDB.Client.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.OAuth;
using SanteDB.Rest.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// Core OAuth/OIDC Client Implementation. This instance does not rely on any upstream or realm settings to function. For an upstream-backed OAuth client, use <see cref="OAuthClient"/> instead.
    /// </summary>
    public class OAuthClientCore : IOAuthClient
    {

        /// <summary>
        /// Gets or sets the token validation parameters
        /// </summary>
        protected TokenValidationParameters TokenValidationParameters { get; set; }
        /// <summary>
        /// Gets or sets the discover document fetched from the server
        /// </summary>
        protected OpenIdConnectDiscoveryDocument DiscoveryDocument { get; set; }

        /// <summary>
        /// Gets the tracer to use for logging
        /// </summary>
        protected Tracer Tracer { get; }

        /// <summary>
        /// Gets the token handler
        /// </summary>
        protected JsonWebTokenHandler TokenHandler { get; }

        /// <summary>
        /// Gets or sets the configured random number generator
        /// </summary>
        protected System.Security.Cryptography.RandomNumberGenerator CryptoRNG { get; set; }

        /// <summary>
        /// Gets the <see cref="IRestClientFactory"/> service which is injected into this service
        /// </summary>
        protected IRestClientFactory RestClientFactory { get; }

        /// <summary>
        /// The retry times that are cached from <see cref="GetRetryWaitTimes"/>.
        /// </summary>
        protected int[] _RetryTimes;

        /// <summary>
        /// The ClientId of the application.
        /// </summary>
        public string ClientId { get; set; }


        /// <summary>
        /// DI constructor
        /// </summary>
        public OAuthClientCore(IRestClientFactory restClientFactory)
        {
            Tracer = new Tracer(GetType().Name);
            CryptoRNG = System.Security.Cryptography.RandomNumberGenerator.Create();
            TokenHandler = new JsonWebTokenHandler();
            RestClientFactory = restClientFactory;
            _RetryTimes = GetRetryWaitTimes();
        }

        /// <summary>
        /// Gets a nonce value that is generated from the CSRNG in .NET and conforms to the OIDC specification.
        /// </summary>
        /// <returns></returns>
        protected virtual string GetNonce()
        {
            byte[] entropy = new byte[32];
            CryptoRNG.GetBytes(entropy);

            return Base64UrlEncoder.Encode(entropy);
        }

        /// <summary>
        /// Gets the rest client from the factory for the auth provider (oauth)
        /// </summary>
        /// <returns></returns>
        protected virtual IRestClient GetRestClient()
        {
            // We want to return a new rest client for each request since the .Credentials would bleed between requests
            return RestClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AuthenticationService);
        }

        /// <summary>
        /// Authenticate a user using the <paramref name="username"/> and <paramref name="password"/>
        /// </summary>
        public virtual IClaimsPrincipal AuthenticateUser(string username, string password, string clientId = null, string tfaSecret = null)
        {
            var request = new OAuthClientTokenRequest
            {
                GrantType = "password",
                Username = username,
                Password = password,
                ClientId = clientId,
                Nonce = GetNonce(),
                MfaCode = tfaSecret
            };

            return GetPrincipal(request);
        }

        /// <summary>
        /// Gets a <see cref="IClaimsPrincipal"/> using the <paramref name="request"/> provided
        /// </summary>
        /// <param name="request">The oauth token request to be sent to the server</param>
        /// <returns>The <see cref="IClaimsPrincipal"/> which was generated from the token response from the server</returns>
        protected virtual IClaimsPrincipal GetPrincipal(OAuthClientTokenRequest request)
        {
            var response = GetToken(request);

            if (null == response?.AccessToken)
            {
                Tracer.TraceError("Received empty access token in the response");
                return null;
            }

            if (response.Nonce != request.Nonce)
            {
                Tracer.TraceError("Received response with nonce that does not match request.");
                return null;
            }

            return CreatePrincipalFromResponse(response);
        }

        /// <summary>
        /// Map claims from the <paramref name="tokenValidationResult"/> into <paramref name="claims"/>
        /// </summary>
        /// <param name="tokenValidationResult">The token validation result from the identity token for claims</param>
        /// <param name="response">The response which was received from the server</param>
        /// <param name="claims">The claims which were mapped</param>
        protected virtual void MapClaims(TokenValidationResult tokenValidationResult, OAuthClientTokenResponse response, List<IClaim> claims) { }

        /// <summary>
        /// Gets an array of wait times (in milliseconds) to wait during a retry operation. The size of the returned array denotes how many times to retry. This is used by <see cref="ExecuteWithRetry{T}(Func{T}, Func{Exception, bool})"/>.
        /// </summary>
        /// <returns>An array of integers for waiting during retry operations.</returns>
        protected virtual int[] GetRetryWaitTimes()
        {
#if DEBUG //In debug, we want to fail quickly to aid in debugging.
            return new[] { 1, 10 };
#else
            return new[] { 1, 10, 100, 1000, 1500 };
#endif
        }
        /// <summary>
        /// Executes <paramref name="func"/> with retry specified in <see cref="GetRetryWaitTimes"/>, sleeping the thread in between.
        /// </summary>
        /// <typeparam name="T">The result type of func.</typeparam>
        /// <param name="func">The callback to execute and retry.</param>
        /// <param name="errorCallback">An optional callback for when an exception ocurrs. This will typically log an error of some kind.</param>
        /// <returns>The result of the call or null.</returns>
        protected virtual T ExecuteWithRetry<T>(Func<T> func, Func<Exception, bool> errorCallback = null) where T: class
        {
            if ( null == errorCallback)
            {
                errorCallback = ex => true;
            }

            T result = null;

            int c = 0;
            while(result == null && c < _RetryTimes.Length)
            {
                try
                {
                    result = func();
                }
                catch(Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    if (!errorCallback(ex))
                    {
                        break;
                    }
                    Thread.Sleep(_RetryTimes[c]);
                }
                c++;
            }
            return result;
        }

        /// <summary>
        /// Set the token validation parameter to be used 
        /// </summary>
        protected virtual void SetTokenValidationParameters()
        {
            var discoverydocument = GetDiscoveryDocument();

            if(discoverydocument == null)
            {
                return;
            }

            TokenValidationParameters = TokenValidationParameters ?? new TokenValidationParameters();

            TokenValidationParameters.ValidIssuers = new[] { discoverydocument.Issuer };
            TokenValidationParameters.ValidAudiences = new[] { ClientId };
            TokenValidationParameters.ValidateAudience = true;
            TokenValidationParameters.ValidateIssuer = true;
            TokenValidationParameters.ValidateIssuerSigningKey = true;
            TokenValidationParameters.ValidateLifetime = true;
            TokenValidationParameters.TryAllIssuerSigningKeys = true;

            var jwksendpoint = discoverydocument.SigningKeyEndpoint;

            TokenValidationParameters.IssuerSigningKeys = GetJsonWebKeySet(jwksendpoint)?.GetSigningKeys();

            TokenValidationParameters.NameClaimType = GetNameClaimType();
        }

        /// <summary>
        /// Retrieves the claim type that is used for name validation in the <see cref="TokenValidationParameters"/>.
        /// </summary>
        /// <returns>A claim type for the name claim.</returns>
        protected virtual string GetNameClaimType()
        {
            if (ClaimMapper.Current.TryGetMapper(ClaimMapper.ExternalTokenTypeJwt, out var mappers))
            {
                foreach (var mapper in mappers)
                {
                    var mapped = mapper.MapToExternalClaimType(SanteDBClaimTypes.DefaultNameClaimType);

                    if (mapped != SanteDBClaimTypes.DefaultNameClaimType)
                    {
                        return mapped;
                    }
                }
            }

            return OAuthConstants.ClaimType_Name;
        }

        /// <summary>
        /// Get the JWKS information from the server
        /// </summary>
        /// <param name="jwksEndpoint">The endpoint from which the JWKS data should be fetched</param>
        /// <returns>The <see cref="JsonWebKeySet"/> from the server</returns>
        protected virtual JsonWebKeySet GetJsonWebKeySet(string jwksEndpoint)
        {
            var restclient = GetRestClient();

            SetupRestClientForJwksRequest(restclient);

            var jwksjson = ExecuteWithRetry(() =>
            {
                var bytes = restclient.Get(jwksEndpoint);

                if (null == bytes)
                    return null;

                return Encoding.UTF8.GetString(bytes);
            }, ex =>
            {
                Tracer.TraceInfo("Exception getting jwks endpoint: {0}", ex);
                return true;
            });

            if (null == jwksjson)
            {
                Tracer.TraceError("Failed to fetch jwks endpoint data from OAuth service.");
            }

            var jwks = new JsonWebKeySet(jwksjson);

            // This needs to be false for HS256 keys
            jwks.SkipUnresolvedJsonWebKeys = false;

            return jwks;
        }

        /// <summary>
        /// Create a principal from the <paramref name="response"/>
        /// </summary>
        /// <param name="response">The token response from the OAUTH server</param>
        /// <returns>The <see cref="IClaimsPrincipal"/> which was created from the <paramref name="response"/></returns>
        protected virtual IClaimsPrincipal CreatePrincipalFromResponse(OAuthClientTokenResponse response)
        {
#if DEBUG
            // Allow PII to be included in exceptions
            IdentityModelEventSource.ShowPII = true;
#endif

            var tokenvalidationresult = TokenHandler.ValidateToken(response.IdToken, TokenValidationParameters);

            if (tokenvalidationresult?.IsValid != true)
            {
                // HACK: Sometimes on startup the discovery document wasn't downloaded properly so attempt to locate this information
                if(String.IsNullOrEmpty(this.TokenValidationParameters.ValidIssuer) && this.TokenValidationParameters.ValidIssuers == null)
                {
                    this.DiscoveryDocument = null;
                    this.SetTokenValidationParameters();
                    return this.CreatePrincipalFromResponse(response);
                }
                throw tokenvalidationresult.Exception ?? new SecurityTokenException("Token validation failed");
            }

            // Map claims from any external format to the internal format
            if (ClaimMapper.Current.TryGetMapper(ClaimMapper.ExternalTokenTypeJwt, out var mappers))
            {
                var claims = mappers.SelectMany(o => o.MapToInternalIdentityClaims(tokenvalidationresult.Claims)).ToList();

                MapClaims(tokenvalidationresult, response, claims);

                return new OAuthClaimsPrincipal(response.AccessToken, tokenvalidationresult.SecurityToken, response.TokenType, response.RefreshToken, response.ExpiresIn, claims);

            }
            else
            {
                throw new InvalidOperationException(); // TODO: Think of a good error to throw here
            }

        }

        /// <summary>
        /// Setup the <paramref name="restClient"/> for a token request
        /// </summary>
        protected virtual void SetupRestClientForTokenRequest(IRestClient restClient) { }

        /// <summary>
        /// Setup the <paramref name="restClient"/> for a JWKS fetch request
        /// </summary>
        protected virtual void SetupRestClientForJwksRequest(IRestClient restClient) { }

        /// <summary>
        /// Send the <paramref name="request"/> to the OAUTH server and return the <see cref="OAuthClientTokenResponse"/>
        /// </summary>
        /// <param name="request">The request which is to be sent to the OAauth Server</param>
        /// <returns>The response from the OAUTH server</returns>
        protected virtual OAuthClientTokenResponse GetToken(OAuthClientTokenRequest request)
        {
            if (null == TokenValidationParameters)
            {
                Tracer.TraceVerbose("Token Validation Parameters have not been set. Calling SetTokenValidationParameters()");
                SetTokenValidationParameters();
            }

            if (null == request.ClientId)
            {
                request.ClientId = ClientId;
            }

            var restclient = GetRestClient();
            // Copy inbound client claims to the claims that the server is getting (purpose of use, overrride, etc.)
            SetupRestClientForTokenRequest(restclient);

            return restclient.Post<OAuthClientTokenRequest, OAuthClientTokenResponse>(GetTokenEndpoint(), "application/x-www-form-urlencoded", request);
        }

        private string GetTokenEndpoint()
        {
            var doc = GetDiscoveryDocument();

            return doc?.TokenEndpoint;
        }

        /// <summary>
        /// Setup the <paramref name="restClient"/> for a discovery endpoint request
        /// </summary>
        protected virtual void SetupRestClientForDiscoveryRequest(IRestClient restClient) { }

        /// <summary>
        /// Get the <see cref="OpenIdConnectDiscoveryDocument"/> from the remote OAUTH server
        /// </summary>
        /// <returns>The configured <see cref="OpenIdConnectDiscoveryDocument"/> which was emitted by the OAUTH server</returns>
        protected virtual OpenIdConnectDiscoveryDocument GetDiscoveryDocument()
        {
            if (null != DiscoveryDocument)
            {
                return DiscoveryDocument;
            }

            var restclient = GetRestClient();

            SetupRestClientForDiscoveryRequest(restclient);

            DiscoveryDocument = ExecuteWithRetry(() =>
            {
                return restclient.Get<OpenIdConnectDiscoveryDocument>(".well-known/openid-configuration");
            },
            ex =>
            {
                Tracer.TraceError("Exception fetching discovery document: {0}", ex);
                return true;
            });

            return DiscoveryDocument;
        }

        /// <summary>
        /// Create an authenticated <see cref="IClaimsPrincipal"/> using a client credential
        /// </summary>
        /// <param name="clientId">The client identifier to send to the oauth server</param>
        /// <param name="clientSecret">The provided client secret</param>
        /// <returns>The authenticated claims principal</returns>
        public IClaimsPrincipal AuthenticateApp(string clientId, string clientSecret = null)
        {
            var request = new OAuthClientTokenRequest
            {
                GrantType = "client_credentials",
                ClientId = clientId,
                ClientSecret = clientSecret,
                Nonce = GetNonce()
            };

            return GetPrincipal(request);
        }

        /// <summary>
        /// Issues a refresh token request to the OAUTH server
        /// </summary>
        /// <param name="refreshToken">The refresh token to be extended</param>
        /// <returns>The updated <see cref="IClaimsPrincipal"/> with the extended session</returns>
        public IClaimsPrincipal Refresh(string refreshToken)
        {
            var request = new OAuthClientTokenRequest
            {
                GrantType = "refresh_token",
                RefreshToken = refreshToken,
                Nonce = GetNonce()
            };

            return GetPrincipal(request);
        }

        #region IDisposable
        private bool disposedValue;

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    CryptoRNG?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~OAuthClientCore()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Perform a <c>x_challenge</c> authentication request against the server
        /// </summary>
        public IClaimsPrincipal ChallengeAuthenticateUser(string userName, Guid challengeKey, string challengeResponse, string clientId = null, string tfaSecret = null)
        {
            var request = new OAuthClientTokenRequest
            {
                Username = userName,
                GrantType = "x_challenge",
                ClientId = clientId,
                Challenge = challengeKey.ToString(),
                Response = challengeResponse,
                MfaCode = tfaSecret,
                Nonce = GetNonce(),
                Scope = PermissionPolicyIdentifiers.LoginPasswordOnly
            };

            return GetPrincipal(request);
        }
        #endregion
    }
}
