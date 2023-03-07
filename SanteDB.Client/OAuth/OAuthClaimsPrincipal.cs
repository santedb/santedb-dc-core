using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// Token claims principal.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OAuthClaimsPrincipal : SanteDBClaimsPrincipal, ITokenPrincipal
    {
        public DateTimeOffset ExpiresAt { get; }
        public DateTimeOffset RenewAfter { get; }

        readonly string _TokenType;
        readonly SecurityToken _IdToken;
        readonly string _AccessToken;
        readonly string _RefreshToken;

        public bool NeedsRenewal => DateTimeOffset.Now >= RenewAfter;
        public bool IsExpired => DateTimeOffset.Now >= ExpiresAt;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenClaimsPrincipal"/> class.
        /// </summary>
        /// <param name="idToken">Token.</param>
        /// <param name="tokenType">Token type.</param>
        public OAuthClaimsPrincipal(string accessToken, SecurityToken idToken, string tokenType, string refreshToken, int expiresIn, List<IClaim> claims) : base()
        {
            if (null == idToken)
            {
                throw new ArgumentNullException(nameof(idToken));
            }
            else if (String.IsNullOrEmpty(tokenType))
            {
                throw new ArgumentNullException(nameof(tokenType));
            }
            else if (tokenType != "urn:ietf:params:oauth:token-type:jwt" &&
                tokenType != "bearer")
            {
                throw new ArgumentOutOfRangeException(nameof(tokenType), "expected urn:ietf:params:oauth:token-type:jwt or bearer");
            }

            _TokenType = tokenType;
            // Token
            _IdToken = idToken;
            this._AccessToken = accessToken;

            _RefreshToken = refreshToken;

            this.AddIdentity(new OAuthTokenIdentity(_IdToken, "OAUTH", true, claims));

            this.ExpiresAt = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(expiresIn));
            this.RenewAfter = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(expiresIn / 2));
            
        }

        /// <summary>
        /// Represent the token claims principal as a string (the access token itself)
        /// </summary>
        /// <returns>To be added.</returns>
        /// <remarks>To be added.</remarks>
        public override string ToString()
        {
            return this._AccessToken;
        }

        public bool CanRefresh => !string.IsNullOrEmpty(_RefreshToken);

        string ITokenPrincipal.AccessToken => this._AccessToken;

        string ITokenPrincipal.TokenType => this._TokenType;

        String ITokenPrincipal.IdentityToken => (this._IdToken as JsonWebToken).EncodedToken;

        String ITokenPrincipal.RefreshToken => this._RefreshToken;

        public string GetRefreshToken() => _RefreshToken;
        public string GetAccessToken() => _AccessToken;

    }
}
