using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using SanteDB.Client.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.OAuth;
using SanteDB.Core.Services;
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
        IRestClient _AuthRestClient;

        // Claim map
        private static readonly Dictionary<String, String> s_ClaimMap = new Dictionary<string, string>() {
            { "name", SanteDBClaimTypes.DefaultNameClaimType },
            { "role", SanteDBClaimTypes.DefaultRoleClaimType },
            { "sub", SanteDBClaimTypes.Sid },
            { "exp", SanteDBClaimTypes.Expiration },
            { "iat", SanteDBClaimTypes.AuthenticationInstant },
            { "email", SanteDBClaimTypes.Email },
            { "phone_number", SanteDBClaimTypes.Telephone }
        };
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
            [JsonProperty("expirs_in")]
            public int ExpiresIn { get; set; }
            [JsonProperty("nonce")]
            public string Nonce { get; set; }
        }

        private string GetNonce()
        {
            var csrng = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] entropy = new byte[32];
            csrng.GetBytes(entropy);

            return Convert.ToBase64String(entropy);
        }

        public OAuthClient(IUpstreamManagementService upstreamManagement, ILocalizationService localization, IRestClientFactory restClientFactory)
        {
            _Tracer = new Tracer(nameof(OAuthClient));
            _UpstreamManagement = upstreamManagement;
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
            if (_UpstreamManagement.IsConfigured())
            {
                var discoverydocument = GetDiscoveryDocument();

                _TokenValidationParameters.ValidIssuers = new[] { discoverydocument.Issuer };
                _TokenValidationParameters.ValidAudiences = new[] { _RealmSettings.LocalClientName };
                _TokenValidationParameters.ValidateAudience = true;
                _TokenValidationParameters.ValidateIssuer = true;
                _TokenValidationParameters.ValidateLifetime = true;
                _TokenValidationParameters.TryAllIssuerSigningKeys = true;

                var jwksendpoint = discoverydocument.SigningKeyEndpoint;

                var restclient = GetRestClient();

                var bytes = restclient.Get(jwksendpoint);

                var jwksjson = Encoding.UTF8.GetString(bytes);

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

        protected virtual void UpstreamRealmChanged(object sender, EventArgs eventArgs)
        {
            try
            {
                //Removed cached client and discovery document.
                _AuthRestClient = null;
                _DiscoveryDocument = null;
                _Tracer.TraceVerbose("Getting new Upstream Realm Settings.");
                _RealmSettings = _UpstreamManagement?.GetSettings();
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

        private static IClaim CreateClaimFromResponse(string type, string value)
        {
            if (s_ClaimMap.ContainsKey(type)){
                type = s_ClaimMap[type];
            }

            if (type == SanteDBClaimTypes.SanteDBScopeClaim && value.StartsWith("ua."))
            {
                value = $"{PermissionPolicyIdentifiers.UnrestrictedAll}{value.Substring(2)}";
            }

            return new SanteDBClaim(type, value);
        }

        private IClaimsPrincipal CreatePrincipalFromResponse(OAuthTokenResponse response)
        {
            var tokenvalidationresult = _TokenHandler.ValidateToken(response.IdToken, _TokenValidationParameters);

            if (tokenvalidationresult?.IsValid != true)
            {
                throw tokenvalidationresult.Exception ?? new SecurityTokenException("Token validation failed");
            }

            var claims = new List<IClaim>();

            foreach (var claim in tokenvalidationresult.Claims)
            {
                if (null == claim.Value)
                {
                    continue;
                }
                else if (claim.Value is string s)
                {
                    claims.Add(CreateClaimFromResponse(claim.Key, s));
                }
                else if (claim.Value is string[] sarr)
                {
                    claims.AddRange(sarr.Select(a => CreateClaimFromResponse(claim.Key, a)));
                }
                else if (claim.Value is IEnumerable<string> enumerable)
                {
                    claims.AddRange(enumerable.Select(v => CreateClaimFromResponse(claim.Key, v)));
                }
                else if (claim.Value is IEnumerable<object> objenumerable)
                {
                    claims.AddRange(objenumerable.Select(v => CreateClaimFromResponse(claim.Key, v.ToString())));
                }
                else
                {
                    claims.Add(CreateClaimFromResponse(claim.Key, claim.Value.ToString()));
                }
            }

            //Drop the realm into the claims so upstream knows which realm this principal is from.
            claims.Add(new SanteDBClaim(SanteDBClaimTypes.Realm, _RealmSettings.Realm.ToString()));

            return new OAuthClaimsPrincipal(response.AccessToken, tokenvalidationresult.SecurityToken, response.TokenType, response.RefreshToken, response.ExpiresIn, claims);
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

            return _AuthRestClient.Post<OAuthTokenRequest, OAuthTokenResponse>(GetTokenEndpoint(), "application/x-www-form-urlencoded", request);
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
            if (null != _AuthRestClient)
            {
                return _AuthRestClient;
            }

            _AuthRestClient = _RestClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AuthenticationService);

            return _AuthRestClient;
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
