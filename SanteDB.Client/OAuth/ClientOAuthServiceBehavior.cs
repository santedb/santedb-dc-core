using Microsoft.IdentityModel.JsonWebTokens;
using RestSrvr;
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;

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
        private readonly ISessionProviderService m_sessionProvider;

        public ClientOAuthServiceBehavior() :base()
        {
            this.m_symmetricEncryptionProvider = ApplicationServiceContext.Current.GetService<ISymmetricCryptographicProvider>();
            this.m_sessionProvider = ApplicationServiceContext.Current.GetService<ISessionProviderService>();
        }

        /// <summary>
        /// ACL Pre-check
        /// </summary>
        public void AclPrecheck(string policyId)
        {
            this.m_policyEnforcementService.Demand(policyId);
        }

        /// <summary>
        /// Signout 
        /// </summary>
        public override object Signout(NameValueCollection formFields)
        {

            // Abandon the UI session
            if(RestOperationContext.Current.Data.TryGetValue(TokenAuthorizationAccessBehavior.RestPropertyNameSession, out object session))
            {
                this.m_sessionProvider.Abandon(session as ISession);
            }
            RestOperationContext.Current.OutgoingResponse.SetCookie(new Cookie("_s", "")
            {
                Discard = true,
                Expired = true,
                Expires = DateTime.Now
            });

            // When the base operation works as well 
            return base.Signout();
        }

        /// <summary>
        /// Token result
        /// </summary>
        public override object Token(NameValueCollection formFields)
        {
            var result = base.Token(formFields);
            if(result is OAuthTokenResponse otr)
            {
                var clientClaims = ClaimsUtility.ExtractClientClaims(RestOperationContext.Current.IncomingRequest.Headers);
                if (!clientClaims.Any(c => c.Type == SanteDBClaimTypes.TemporarySession && c.Value == "true"))
                {
                    RestOperationContext.Current.OutgoingResponse.SetCookie(new Cookie("_s", otr.AccessToken, "/")
                    {
                        HttpOnly = true,
                        Expires = DateTime.Now.AddSeconds(otr.ExpiresIn).ToUniversalTime(),
                        Expired = false
                    });
                }
            }
            return result;
        }
    }
}
