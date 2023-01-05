using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Rest.OAuth.Abstractions;
using SanteDB.Rest.OAuth.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.OAuth
{
    public class OAuthCookieRefreshTokenHandler : ITokenRequestHandler
    {
        readonly ISessionTokenResolverService _SessionResolver;
        readonly ISessionIdentityProviderService _SessionIdentityProvider;
        readonly IAuditService _AuditService;
        readonly Tracer _Tracer;

        public IEnumerable<string> SupportedGrantTypes => new[] { OAuthClientConstants.GRANTTYPE_REFRESHCOOKIE };

        public string ServiceName => "OAuth Client Cookie Refersh Token Handler";

        public OAuthCookieRefreshTokenHandler(ISessionTokenResolverService sessionResolver, ISessionIdentityProviderService sessionIdentityProvider, IAuditService auditService)
        {
            _Tracer = new Tracer(nameof(OAuthCookieRefreshTokenHandler));
            _SessionResolver = sessionResolver;
            _SessionIdentityProvider = sessionIdentityProvider;
            _AuditService = auditService;
        }

        public bool HandleRequest(OAuthTokenRequestContext context)
        {
            var cookie = context.IncomingRequest.Cookies["_r"];

            if (null == cookie)
            {
                context.ErrorMessage = "missing cookie";
                context.ErrorType = OAuthErrorType.invalid_grant;
                return false;
            }

            try
            {
                context.Session = _SessionResolver.ExtendSessionWithRefreshToken(cookie.Value);

                var principal = _SessionIdentityProvider.Authenticate(context.Session);

                _AuditService.Audit().ForSessionStart(context.Session, principal, null != context.Session).Send();

                if (null == context.Session)
                {
                    _Tracer.TraceInfo("Failed to initialize session from refresh token.");
                    context.ErrorType = OAuthErrorType.invalid_grant;
                    context.ErrorMessage = "invalid refresh token";
                }

                return true;
            }
            catch(SecuritySessionException ex)
            {
                _Tracer.TraceInfo("Failed to initialize session from refresh cookie: {0}", ex.ToString());
                context.ErrorType = OAuthErrorType.invalid_grant;
                context.ErrorMessage = "invalid cookie";
                return false;
            }
        }
    }
}
