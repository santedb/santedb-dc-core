/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using RestSrvr.Attributes;
using SanteDB.Core;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Fault;
using SanteDB.Rest.Common.Security;
using SanteDB.Rest.OAuth.Model;
using SanteDB.Rest.OAuth.Rest;
using System;
using System.Linq;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// Service contract for OAUTH
    /// </summary>
    public interface IClientOAuthServiceContract : IOAuthServiceContract
    {

        /// <summary>
        /// Demands <paramref name="policyId"/> from the current user
        /// </summary>
        [Get("pdp/{policyId}")]
        [ServiceFault(204, typeof(object), "The user has the authorization demand and can access")]
        [ServiceFault(401, typeof(RestServiceFault), "The user needs to re-authenticate (elevate)")]
        [ServiceFault(403, typeof(RestServiceFault), "The user does not have access to this policy")]
        void AclPrecheck(String policyId);

    }

    /// <summary>
    /// An extension of the <see cref="OAuthServiceBehavior"/> which adds and removes cookies
    /// </summary>
    public class ClientOAuthServiceBehavior : OAuthServiceBehavior, IClientOAuthServiceContract
    {

        private readonly ISymmetricCryptographicProvider m_symmetricEncryptionProvider;

        /// <summary>
        /// 
        /// </summary>
        public ClientOAuthServiceBehavior() : base()
        {
            this.m_symmetricEncryptionProvider = ApplicationServiceContext.Current.GetService<ISymmetricCryptographicProvider>();
        }

        /// <summary>
        /// ACL Pre-check
        /// </summary>
        public void AclPrecheck(string policyId)
        {
            this.m_policyEnforcementService.Demand(policyId);
        }

        /// <inheritdoc />
        protected override void OnAfterSignOut(OAuthSignoutRequestContext context)
        {

            // HACK: Chrome does not like the way SetCookie works for the two cookies so we're going to clear them manually
            foreach (var session in context.AbandonedSessions)
            {
                // TODO: Propogate to central iCDR 
                if (!session.Claims.Any(c => c.Type == SanteDBClaimTypes.TemporarySession && Boolean.TryParse(c.Value, out var tempSession) && tempSession))
                {
                    context.OutgoingResponse.AppendHeader("Set-Cookie", $"{ExtendedCookieNames.RefreshCookieName}=; Max-Age=0; Path={GetAuthPathForCookie(context)}; HttpOnly; Discard");
                    context.OutgoingResponse.AppendHeader("Set-Cookie", $"{ExtendedCookieNames.SessionCookieName}=; Max-Age=0; Path=/; HttpOnly; Discard");
                }
            }
        }

        /// <inheritdoc />
        protected override void BeforeSendTokenResponse(OAuthTokenRequestContext context, OAuthTokenResponse response)
        {
            var clientClaims = ClaimsUtility.ExtractClientClaims(context.IncomingRequest.Headers);
            if (!clientClaims.Any(c => c.Type == SanteDBClaimTypes.TemporarySession && Boolean.TryParse(c.Value, out var tempSession) && tempSession))
            {
                if (null != response.RefreshToken)
                {
                    context.OutgoingResponse.AppendHeader("Set-Cookie", $"{ExtendedCookieNames.RefreshCookieName}={response.RefreshToken}; Max-Age={response.ExpiresIn}; Path={GetAuthPathForCookie(context)}; HttpOnly; Discard");
                }

                context.OutgoingResponse.AppendHeader("Set-Cookie", $"{ExtendedCookieNames.SessionCookieName}={response.AccessToken}; Max-Age={response.ExpiresIn + 10}; Path=/; HttpOnly; Discard");

            }
        }


        /// <summary>
        /// Helper method to retrieve the path of the auth server for use in a scoped cookie.
        /// </summary>
        /// <param name="context">The context to access the operation context from.</param>
        /// <returns>A string that can be used to scope a cookie to the auth server.</returns>
        private static string GetAuthPathForCookie(OAuthRequestContextBase context)
        {
            if (null == context)
            {
                return null;
            }

            return "/" + context.OperationContext.ServiceEndpoint.Description.ListenUri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
        }

    }
}
