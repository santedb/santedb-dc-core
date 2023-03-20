﻿/*
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
 * Date: 2023-3-10
 */
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SanteDB.BI.Model;
using SanteDB.Client.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.OAuth;
using SanteDB.Rest.OAuth;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        protected TokenValidationParameters TokenValidationParameters { get; set; }
        protected OpenIdConnectDiscoveryDocument DiscoveryDocument { get; set; }

        protected Tracer Tracer { get; }
        protected JsonWebTokenHandler TokenHandler {get;}

        protected System.Security.Cryptography.RandomNumberGenerator CryptoRNG { get; set; }

        protected IRestClientFactory RestClientFactory { get; }

        /// <summary>
        /// The ClientId of the application.
        /// </summary>
        public string ClientId { get; set; }
        

        public OAuthClientCore(IRestClientFactory restClientFactory)
        {
            Tracer = new Tracer(GetType().Name);
            CryptoRNG = System.Security.Cryptography.RandomNumberGenerator.Create();
            TokenHandler = new JsonWebTokenHandler();
            RestClientFactory = restClientFactory;
            
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

        protected virtual void MapClaims(TokenValidationResult tokenValidationResult, OAuthClientTokenResponse response, List<IClaim> claims) { }

        protected virtual void SetTokenValidationParameters()
        {
            var discoverydocument = GetDiscoveryDocument();

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
                foreach(var mapper in mappers)
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

        protected virtual JsonWebKeySet GetJsonWebKeySet(string jwksEndpoint)
        {
            var restclient = GetRestClient();

            SetupRestClientForJwksRequest(restclient);

            int requestcounter = 0;
            string jwksjson = null;

            while (jwksjson == null && (requestcounter++) < 5)
            {
                try
                {
                    //TODO: Our rest client needs a better interface
                    var bytes = restclient.Get(jwksEndpoint);

                    jwksjson = Encoding.UTF8.GetString(bytes);
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    Tracer.TraceInfo("Exception getting jwks endpoint: {0}", ex);
                    Thread.Sleep(1000);
                }
            }

            if (null == jwksjson)
            {
                Tracer.TraceError("Failed to fetch jwks endpoint data from OAuth service.");
            }

            var jwks = new JsonWebKeySet(jwksjson);

            // This needs to be false for HS256 keys
            jwks.SkipUnresolvedJsonWebKeys = false;

            return jwks;
        }

        protected virtual IClaimsPrincipal CreatePrincipalFromResponse(OAuthClientTokenResponse response)
        {
            var tokenvalidationresult = TokenHandler.ValidateToken(response.IdToken, TokenValidationParameters);

            if (tokenvalidationresult?.IsValid != true)
            {
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

        protected virtual void SetupRestClientForTokenRequest(IRestClient restClient) { }
        
        protected virtual void SetupRestClientForJwksRequest(IRestClient restClient) { }

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

            return doc.TokenEndpoint;
        }

        protected virtual void SetupRestClientForDiscoveryRequest(IRestClient restClient) { }

        protected virtual OpenIdConnectDiscoveryDocument GetDiscoveryDocument()
        {
            if (null != DiscoveryDocument)
            {
                return DiscoveryDocument;
            }

            var restclient = GetRestClient();

            SetupRestClientForDiscoveryRequest(restclient);

            int counter = 0;

            while (DiscoveryDocument == null && (counter++) < 5)
            {
                try
                {
                    DiscoveryDocument = restclient.Get<OpenIdConnectDiscoveryDocument>(".well-known/openid-configuration");
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    Thread.Sleep(1000);
                }
            }

            return DiscoveryDocument;
        }

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