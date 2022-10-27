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
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.OAuth
{

    public class OAuthClient : IOAuthClient
    {
        IUpstreamRealmSettings _RealmSettings;
        readonly IUpstreamIntegrationService _UpstreamIntegration;
        readonly ILocalizationService _Localization;
        readonly Tracer _Tracer;
        readonly IRestClientFactory _RestClientFactory;
        IRestClient _AuthRestClient;

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

        public OAuthClient(IUpstreamIntegrationService upstreamIntegration, ILocalizationService localization, IRestClientFactory restClientFactory)
        {
            _Tracer = new Tracer(nameof(OAuthClient));
            _UpstreamIntegration = upstreamIntegration;
            _UpstreamIntegration.RealmChanged += UpstreamRealmChanged;
            _RealmSettings = upstreamIntegration?.GetSettings();
            _Localization = localization;
            _RestClientFactory = restClientFactory;
        }

        protected virtual void UpstreamRealmChanged(object sender, EventArgs eventArgs)
        {
            try
            {
                _AuthRestClient = null;
                _Tracer.TraceVerbose("Getting new Upstream Realm Settings.");
                _RealmSettings = _UpstreamIntegration?.GetSettings();
                _Tracer.TraceVerbose("Successfully updated Upstream Realm Settings.");
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Exception getting Upstream Realm Settings: {0}", ex);
                _RealmSettings = null;
            }
        }

        public IPrincipal AuthenticateUser(string username, string password, string clientId)
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

        private IPrincipal GetPrincipal(OAuthTokenRequest request)
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

            return new OAuthClaimsPrincipal(response.AccessToken, response.IdToken, response.TokenType, response.RefreshToken, response.ExpiresIn);
        }

        private OAuthTokenResponse GetToken(OAuthTokenRequest request)
        {
            if (null == _RealmSettings)
            {
                _Tracer.TraceError("Attempt to authenticate when there is no upstream realm available.");
                throw new InvalidOperationException(_Localization.GetString(ErrorMessageStrings.INVALID_STATE));
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
            var restclient = GetRestClient();

            try
            {
                var document = restclient.Get<OpenIdConnectDiscoveryDocument>(".well-known/openid-configuration");

                return document;
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                return null;
            }
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

        public IPrincipal AuthenticateApp(string clientId, string clientSecret = null)
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
    }
}
