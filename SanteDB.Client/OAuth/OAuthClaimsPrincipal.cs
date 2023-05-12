/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// Token claims principal.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OAuthClaimsPrincipal : SanteDBClaimsPrincipal, ITokenPrincipal
    {
        /// <summary>
        /// Gets the time that the principal will be expired
        /// </summary>
        public DateTimeOffset ExpiresAt { get; }
        /// <summary>
        /// Gets the time that the principal should be renewed
        /// </summary>
        public DateTimeOffset RenewAfter { get; }

        readonly string _TokenType;
        readonly SecurityToken _IdToken;
        readonly string _AccessToken;
        readonly string _RefreshToken;

        /// <summary>
        /// Gets whether the principal needs to be renewed
        /// </summary>
        public bool NeedsRenewal => DateTimeOffset.Now >= RenewAfter;
        /// <summary>
        /// Gets whether the principal is expired
        /// </summary>
        public bool IsExpired => DateTimeOffset.Now >= ExpiresAt;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthClaimsPrincipal"/> class.
        /// </summary>
        /// <param name="idToken">The identity token</param>
        /// <param name="tokenType">The type of identity token provided</param>
        /// <param name="accessToken">The token to access the resource</param>
        /// <param name="claims">The claims attached to the principal</param>
        /// <param name="expiresIn">The time when this principal is expired</param>
        /// <param name="refreshToken">The token which can be used to extend this session</param>
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

        /// <summary>
        /// True if the principal can be refreshed
        /// </summary>
        public bool CanRefresh => !string.IsNullOrEmpty(_RefreshToken);

        /// <summary>
        /// Gets the access token
        /// </summary>
        string ITokenPrincipal.AccessToken => this._AccessToken;

        /// <summary>
        /// Gets the token type
        /// </summary>
        string ITokenPrincipal.TokenType => this._TokenType;

        /// <summary>
        /// Gets the identity token
        /// </summary>
        String ITokenPrincipal.IdentityToken => (this._IdToken as JsonWebToken).EncodedToken;

        /// <summary>
        /// Gets the refresh token
        /// </summary>
        String ITokenPrincipal.RefreshToken => this._RefreshToken;

        /// <summary>
        /// Gets the refresh token
        /// </summary>
        public string GetRefreshToken() => _RefreshToken;

        /// <summary>
        /// Gets the access token
        /// </summary>
        /// <returns></returns>
        public string GetAccessToken() => _AccessToken;

    }
}
