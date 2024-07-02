/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-6-21
 */
using Microsoft.IdentityModel.Tokens;
using RestSrvr;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Services;
using SanteDB.Rest.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.OAuth
{

    /// <summary>
    /// An implementation of the OAuth client
    /// </summary>
    public class OAuthClient : OAuthClientCore
    {
        IUpstreamRealmSettings _RealmSettings;
        readonly IUpstreamManagementService _UpstreamManagement;
        readonly ILocalizationService _Localization;
        //IRestClient _AuthRestClient;


        /// <summary>
        /// DI constructor
        /// </summary>
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

        /// <summary>
        /// Maps the claims from the <paramref name="tokenValidationResult"/> to the <paramref name="claims"/>
        /// </summary>
        /// <param name="tokenValidationResult">The token validation result to map claims for</param>
        /// <param name="response">The OAUTH server response</param>
        /// <param name="claims">The claims to be mapped</param>
        protected override void MapClaims(TokenValidationResult tokenValidationResult, OAuthClientTokenResponse response, List<IClaim> claims)
        {
            base.MapClaims(tokenValidationResult, response, claims);

            //Drop the realm into the claims so upstream knows which realm this principal is from.
            claims.Add(new SanteDBClaim(SanteDBClaimTypes.Realm, _RealmSettings.Realm.ToString()));
        }

        /// <summary>
        /// Set the token validation parameters based on the configuration
        /// </summary>
        protected override void SetTokenValidationParameters()
        {
            if (null != _RealmSettings) // This may be called before the UpstreamManagementService is fully configured - i.e. is configuring
            {
                base.SetTokenValidationParameters();
                if (null != TokenValidationParameters)
                {
                    TokenValidationParameters.ValidAudiences = new[] { _RealmSettings.LocalClientName, _RealmSettings.LocalDeviceName };
                }
                else
                {
                    Tracer.TraceInfo("Unable to retrieve token validation parameters from upstream service.");
                }
                ClientId = _RealmSettings.LocalClientName;
            }
            else
            {
                Tracer.TraceWarning("Upstream is not yet configured - skipping fetch of token validation parameters");
            }
        }

        /// <summary>
        /// handler for when the upstream realm has changed
        /// </summary>
        protected virtual void UpstreamRealmChanging(object sender, UpstreamRealmChangedEventArgs eventArgs)
        {
            try
            {
                //Removed cached client and discovery document and rediscover with the new (soon to be joined realm)
                Tracer.TraceVerbose("Removing upstream realm settings due to upstream realm changing notification.");
                DiscoveryDocument = null;
                ClientId = null;
                _RealmSettings = eventArgs.UpstreamRealmSettings;
                SetTokenValidationParameters();
                Tracer.TraceVerbose("Removed upstream realm settings due to upstream realm changing notification.");

            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                Tracer.TraceError("Exception removing upstream realm settings: {0}", ex);
                _RealmSettings = null;
            }
        }

        /// <summary>
        /// Event handler when the upstream realm has changed
        /// </summary>
        protected virtual void UpstreamRealmChanged(object sender, UpstreamRealmChangedEventArgs eventArgs)
        {
            try
            {
                Tracer.TraceVerbose("Getting new upstream realm settings due to upstream realm changed notification.");
                _RealmSettings = eventArgs.UpstreamRealmSettings;
                ClientId = _RealmSettings.LocalClientName;
                Tracer.TraceVerbose("Successfully updated upstream realm settings due to upstream realm changed notification.");
                SetTokenValidationParameters();
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                Tracer.TraceError("Exception getting Upstream Realm Settings: {0}", ex);
                _RealmSettings = null;
            }
        }

        /// <summary>
        /// Contacts the OAUTH server with <paramref name="request"/>
        /// </summary>
        /// <param name="request">The OAUTH authentication request to send to the server</param>
        /// <returns>The response provided by the OAUTH server</returns>
        protected override OAuthClientTokenResponse GetToken(OAuthClientTokenRequest request, IEnumerable<IClaim> clientClaimAssertions = null)
        {
            if (null == _RealmSettings)
            {
                Tracer.TraceError("Attempt to authenticate when there is no upstream realm available.");
                throw new InvalidOperationException(_Localization.GetString(ErrorMessageStrings.INVALID_STATE));
            }

            return base.GetToken(request);
        }

        /// <summary>
        /// Setup this class to send a token request
        /// </summary>
        protected override void SetupRestClientForTokenRequest(IRestClient restClient, IEnumerable<IClaim> clientClaimAssertions = null)
        {
            base.SetupRestClientForTokenRequest(restClient, clientClaimAssertions);
            restClient.Requesting += (o, e) =>
            {
                var clientClaimHeader = RestOperationContext.Current?.IncomingRequest.Headers[ExtendedHttpHeaderNames.BasicHttpClientClaimHeaderName];
                if (!String.IsNullOrEmpty(clientClaimHeader))
                {
                    e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.BasicHttpClientClaimHeaderName, clientClaimHeader);
                }
                if(clientClaimAssertions != null)
                {
                    var claimHeaderValue = String.Join(";", clientClaimAssertions.Select(x => $"{x.Type}={x.Value}"));
                    e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.BasicHttpClientClaimHeaderName, Convert.ToBase64String(Encoding.UTF8.GetBytes(claimHeaderValue)));
                }
            };
        }

    }
}
