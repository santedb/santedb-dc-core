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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace SanteDB.Client.OAuth
{

    public class OAuthClient : OAuthClientCore
    {
        IUpstreamRealmSettings _RealmSettings;
        readonly IUpstreamManagementService _UpstreamManagement;
        readonly ILocalizationService _Localization;
        //IRestClient _AuthRestClient;


        public OAuthClient(IUpstreamManagementService upstreamManagement, ILocalizationService localization, IRestClientFactory restClientFactory)
            : base(restClientFactory)
        {
            _UpstreamManagement = upstreamManagement;
            _UpstreamManagement.RealmChanging += UpstreamRealmChanging;
            _UpstreamManagement.RealmChanged += UpstreamRealmChanged;
            _RealmSettings = upstreamManagement?.GetSettings();
            _Localization = localization;
            //SetTokenValidationParameters();
        }

        protected override void MapClaims(TokenValidationResult tokenValidationResult, OAuthClientTokenResponse response, List<IClaim> claims)
        {
            base.MapClaims(tokenValidationResult, response, claims);

            //Drop the realm into the claims so upstream knows which realm this principal is from.
            claims.Add(new SanteDBClaim(SanteDBClaimTypes.Realm, _RealmSettings.Realm.ToString()));
        }

        protected override void SetTokenValidationParameters()
        {
            if (null != _RealmSettings) // This may be called before the UpstreamManagementService is fully configured - i.e. is configuring
            {
                base.SetTokenValidationParameters();
            }
            else
            {
                Tracer.TraceWarning("Upstream is not yet configured - skipping fetch of token validation parameters");
            }
        }

        protected virtual void UpstreamRealmChanging(object sender, UpstreamRealmChangedEventArgs eventArgs)
        {
            try
            {
                //Removed cached client and discovery document and rediscover with the new (soon to be joined realm)
                DiscoveryDocument = null;
                ClientId = null;
                _RealmSettings = eventArgs.UpstreamRealmSettings;
                SetTokenValidationParameters();

            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                Tracer.TraceError("Exception clearing upstream realm settings: {0}", ex);
                _RealmSettings = null;
            }
        }

        protected virtual void UpstreamRealmChanged(object sender, UpstreamRealmChangedEventArgs eventArgs)
        {
            try
            {
                Tracer.TraceVerbose("Getting new Upstream Realm Settings.");
                _RealmSettings = eventArgs.UpstreamRealmSettings;
                ClientId = _RealmSettings.LocalClientName;
                Tracer.TraceVerbose("Successfully updated Upstream Realm Settings.");
                SetTokenValidationParameters();
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                Tracer.TraceError("Exception getting Upstream Realm Settings: {0}", ex);
                _RealmSettings = null;
            }
        }


        protected override OAuthClientTokenResponse GetToken(OAuthClientTokenRequest request)
        {
            if (null == _RealmSettings)
            {
                Tracer.TraceError("Attempt to authenticate when there is no upstream realm available.");
                throw new InvalidOperationException(_Localization.GetString(ErrorMessageStrings.INVALID_STATE));
            }

            return base.GetToken(request);
        }

        protected override void SetupRestClientForTokenRequest(IRestClient restClient)
        {
            base.SetupRestClientForTokenRequest(restClient);
            restClient.Requesting += (o, e) =>
            {
                var clientClaimHeader = RestOperationContext.Current?.IncomingRequest.Headers[ExtendedHttpHeaderNames.BasicHttpClientClaimHeaderName];
                if (!String.IsNullOrEmpty(clientClaimHeader))
                {
                    e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.BasicHttpClientClaimHeaderName, clientClaimHeader);
                }
            };
        }

    }
}
