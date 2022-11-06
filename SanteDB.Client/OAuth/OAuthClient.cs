using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RestSrvr;
using SanteDB.Client.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.OAuth;
using SanteDB.Core.Services;
using SanteDB.Rest.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace SanteDB.Client.OAuth
{

    public class OAuthClient : IOAuthClient
    {
        IUpstreamRealmSettings _RealmSettings;
        readonly IUpstreamManagementService _UpstreamManagement;
        readonly ILocalizationService _Localization;
        readonly Tracer _Tracer;
        readonly IRestClientFactory _RestClientFactory;
        readonly JsonWebTokenHandler _TokenHandler;
        readonly TokenValidationParameters _TokenValidationParameters;
        OpenIdConnectDiscoveryDocument _DiscoveryDocument;
        //IRestClient _AuthRestClient;

        private class OAuthTokenRequest
        {
            [FormElement("grant_type")]
            public string GrantType { get; set; }
            [FormElement("username")]
            public string Username { get; set; }
            [FormElement("password")]
            public string Password { get; set; }
            [FormElement("client_id")]
            public string ClientId { get; set; }
            [FormElement("client_secret")]
            public string ClientSecret { get; set; }
            [FormElement("nonce")]
            public string Nonce { get; set; }
            [FormElement("refresh_token")]
            public string RefreshToken { get; set; }
        }

        private class OAuthTokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }
            [JsonProperty("id_token")]
            public string IdToken { get; set; }
            [JsonProperty("token_type")]
            public string TokenType { get; set; }
            [JsonProperty("refresh_token")]
            public string RefreshToken { get; set; }
            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }
            [JsonProperty("nonce")]
            public string Nonce { get; set; }
        }

        private string GetNonce()
        {
            var csrng = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] entropy = new byte[32];
            csrng.GetBytes(entropy);

            return Base64UrlEncoder.Encode(entropy);
        }

        public OAuthClient(IUpstreamManagementService upstreamManagement, ILocalizationService localization, IRestClientFactory restClientFactory)
        {
            _Tracer = new Tracer(nameof(OAuthClient));
            _UpstreamManagement = upstreamManagement;
            _UpstreamManagement.RealmChanging += UpstreamRealmChanging;
            _UpstreamManagement.RealmChanged += UpstreamRealmChanged;
            _RealmSettings = upstreamManagement?.GetSettings();
            _Localization = localization;
            _RestClientFactory = restClientFactory;
            _TokenHandler = new JsonWebTokenHandler();
            _TokenValidationParameters = new TokenValidationParameters();
            SetTokenValidationParameters();
        }

        protected virtual void SetTokenValidationParameters()
        {
            if (null != _RealmSettings) // This may be called before the UpstreamManagementService is fully configured - i.e. is configuring
            {
                var discoverydocument = GetDiscoveryDocument();

                _TokenValidationParameters.ValidIssuers = new[] { discoverydocument.Issuer };
                _TokenValidationParameters.ValidAudiences = new[] { _RealmSettings.LocalClientName, _RealmSettings.LocalDeviceName };
                _TokenValidationParameters.ValidateAudience = true;
                _TokenValidationParameters.ValidateIssuer = true;
                _TokenValidationParameters.ValidateLifetime = true;
                _TokenValidationParameters.TryAllIssuerSigningKeys = true;

                var jwksendpoint = discoverydocument.SigningKeyEndpoint;

                var restclient = GetRestClient();

                int requestcounter = 0;
                string jwksjson = null;

                while(jwksjson == null && (requestcounter++) < 5)
                {
                    try
                    {
                        //TODO: Our rest client needs a better interface
                        var bytes = restclient.Get(jwksendpoint);

                        jwksjson = Encoding.UTF8.GetString(bytes);
                    }
                    catch(Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        _Tracer.TraceInfo("Exception getting jwks endpoint: {0}", ex);
                        Thread.Sleep(1000);
                    }
                }

                if (null == jwksjson)
                {
                    _Tracer.TraceError("Failed to fetch jwks endpoint data from OAuth service.");
                }
                
                var jwks = new JsonWebKeySet(jwksjson);

                jwks.SkipUnresolvedJsonWebKeys = true;

                _TokenValidationParameters.IssuerSigningKeys = jwks.Keys;
                _TokenValidationParameters.NameClaimType = "name";
            }
            else
            {
                _Tracer.TraceWarning("Upstream is not yet configured - skipping fetch of token validation parameters");
            }
        }

        protected virtual void UpstreamRealmChanging(object sender, UpstreamRealmChangedEventArgs eventArgs)
        {
            try
            {
                //Removed cached client and discovery document and rediscover with the new (soon to be joined realm)
                _DiscoveryDocument = null;
                _RealmSettings = eventArgs.UpstreamRealmSettings;
                SetTokenValidationParameters();

            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Exception clearing upstream realm settings: {0}", ex);
                _RealmSettings = null;
            }
        }

        protected virtual void UpstreamRealmChanged(object sender, UpstreamRealmChangedEventArgs eventArgs)
        {
            try
            {
                _Tracer.TraceVerbose("Getting new Upstream Realm Settings.");
                _RealmSettings = eventArgs.UpstreamRealmSettings;
                _Tracer.TraceVerbose("Successfully updated Upstream Realm Settings.");
                SetTokenValidationParameters();
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Exception getting Upstream Realm Settings: {0}", ex);
                _RealmSettings = null;
            }
        }

        public IClaimsPrincipal AuthenticateUser(string username, string password, string clientId = null)
        {
            var request = new OAuthTokenRequest
            {
                GrantType = "password",
                Username = username,
                Password = password,
                ClientId = clientId,
                Nonce = GetNonce()
            };

            return GetPrincipal(request);
        }

        private IClaimsPrincipal GetPrincipal(OAuthTokenRequest request)
        {
            var response = GetToken(request);

            if (null == response?.AccessToken)
            {
                _Tracer.TraceError("Received empty access token in the response");
                return null;
            }

            if (response.Nonce != request.Nonce)
            {
                _Tracer.TraceError("Received response with nonce that does not match request.");
                return null;
            }

            return CreatePrincipalFromResponse(response);
        }

       

        private IClaimsPrincipal CreatePrincipalFromResponse(OAuthTokenResponse response)
        {
            var tokenvalidationresult = _TokenHandler.ValidateToken(response.IdToken, _TokenValidationParameters);

            if (tokenvalidationresult?.IsValid != true)
            {
                throw tokenvalidationresult.Exception ?? new SecurityTokenException("Token validation failed");
            }

            // Map claims from any external format to the internal format
            if (ClaimMapper.Current.TryGetMapper(ClaimMapper.ExternalTokenTypeJwt, out var mappers))
            {
                var claims = mappers.SelectMany(o => o.MapToInternalIdentityClaims(tokenvalidationresult.Claims)).ToList();

                //Drop the realm into the claims so upstream knows which realm this principal is from.
                claims.Add(new SanteDBClaim(SanteDBClaimTypes.Realm, _RealmSettings.Realm.ToString()));

                return new OAuthClaimsPrincipal(response.AccessToken, tokenvalidationresult.SecurityToken, response.TokenType, response.RefreshToken, response.ExpiresIn, claims);

            }
            else
            {
                throw new InvalidOperationException(); // TODO: Think of a good error to throw here
            }

        }

        private OAuthTokenResponse GetToken(OAuthTokenRequest request)
        {
            if (null == _RealmSettings)
            {
                _Tracer.TraceError("Attempt to authenticate when there is no upstream realm available.");
                throw new InvalidOperationException(_Localization.GetString(ErrorMessageStrings.INVALID_STATE));
            }

            if (null == request.ClientId)
            {
                request.ClientId = _RealmSettings.LocalClientName;
            }

            var restClient = GetRestClient();
            // Copy inbound client claims to the claims to the claims that the server is getting (purpose of use, overrride, etc.)
            restClient.Requesting += (o, e) =>
            {
                var clientClaimHeader = RestOperationContext.Current?.IncomingRequest.Headers[ExtendedHttpHeaderNames.BasicHttpClientClaimHeaderName];
                if (!String.IsNullOrEmpty(clientClaimHeader))
                {
                    e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.BasicHttpClientClaimHeaderName, clientClaimHeader);
                }
            };

            return restClient.Post<OAuthTokenRequest, OAuthTokenResponse>(GetTokenEndpoint(), "application/x-www-form-urlencoded", request);
        }

        private string GetTokenEndpoint()
        {
            var doc = GetDiscoveryDocument();

            return doc.TokenEndpoint;
        }


        private OpenIdConnectDiscoveryDocument GetDiscoveryDocument()
        {
            if (null != _DiscoveryDocument)
            {
                return _DiscoveryDocument;
            }
            

            var restclient = GetRestClient();

            int counter = 0;

            while (_DiscoveryDocument == null && (counter++) < 5)
            {
                try
                {
                    _DiscoveryDocument = restclient.Get<OpenIdConnectDiscoveryDocument>(".well-known/openid-configuration");
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    Thread.Sleep(1000);
                }
            }

            return _DiscoveryDocument;
        }

        /// <summary>
        /// Gets the rest client from the factory for the auth provider (oauth)
        /// </summary>
        /// <returns></returns>
        private IRestClient GetRestClient()
        {
            // We want to return a new rest client for each request since the .Credentials would bleed between requests
            return _RestClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AuthenticationService);
        }

        public IClaimsPrincipal AuthenticateApp(string clientId, string clientSecret = null)
        {
            var request = new OAuthTokenRequest
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
            var request = new OAuthTokenRequest
            {
                GrantType = "refresh_token",
                RefreshToken = refreshToken,
                Nonce = GetNonce()
            };

            return GetPrincipal(request);
        }
    }
}
