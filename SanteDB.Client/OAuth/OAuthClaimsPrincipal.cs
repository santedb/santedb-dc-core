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
