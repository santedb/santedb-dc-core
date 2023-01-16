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
    /// <summary>
    /// A special token handler for client scenarios where the client does not have access to the refresh token for security purposes. 
    /// </summary>
    /// <example>
    ///     POST /oauth_token HTTP/1.1
    ///     Content-Type: application/json
    ///     
    ///     {
    ///         "grant_type": "x-refresh-cookie"
    ///     }
    /// </example>
    public class OAuthCookieRefreshTokenHandler : ITokenRequestHandler
    {
        readonly ISessionTokenResolverService _SessionResolver;
        readonly ISessionIdentityProviderService _SessionIdentityProvider;
        readonly IAuditService _AuditService;
        readonly Tracer _Tracer;
        /// <inheritdoc />
        public IEnumerable<string> SupportedGrantTypes => new[] { OAuthClientConstants.GRANTTYPE_REFRESHCOOKIE };
        /// <inheritdoc />
        public string ServiceName => "OAuth Client Cookie Refresh Token Handler";
        /// <summary>
        /// Dependency injection constructor
        /// </summary>
        /// <param name="sessionResolver"></param>
        /// <param name="sessionIdentityProvider"></param>
        /// <param name="auditService"></param>
        public OAuthCookieRefreshTokenHandler(ISessionTokenResolverService sessionResolver, ISessionIdentityProviderService sessionIdentityProvider, IAuditService auditService)
        {
            _Tracer = new Tracer(nameof(OAuthCookieRefreshTokenHandler));
            _SessionResolver = sessionResolver;
            _SessionIdentityProvider = sessionIdentityProvider;
            _AuditService = auditService;
        }

        /// <inheritdoc />
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
