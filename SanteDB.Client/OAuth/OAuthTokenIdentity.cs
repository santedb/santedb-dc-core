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
 * Date: 2023-5-19
 */
using Microsoft.IdentityModel.Tokens;
using SanteDB.Core.Security.Claims;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// An implementation of the <see cref="IClaimsIdentity"/> which is constructed from an OAUTH token
    /// </summary>
    public class OAuthTokenIdentity : IClaimsIdentity
    {
        readonly SecurityToken _Token;

        readonly List<IClaim> _Claims;

        /// <summary>
        /// Create a new token identity with the specified data from the oauth response
        /// </summary>
        /// <param name="token">The access token which was in th eresponse</param>
        /// <param name="authenticationType">The authenticate mode </param>
        /// <param name="isAuthenticated">True if the authentication actually occurred</param>
        /// <param name="claims">The claims which were provided for the access token</param>
        internal OAuthTokenIdentity(SecurityToken token, string authenticationType, bool isAuthenticated, List<IClaim> claims)
        {
            _Token = token;
            AuthenticationType = authenticationType;
            IsAuthenticated = isAuthenticated;


            _Claims = claims; // TODO: throw an exception?

        }

        /// <inheritdoc/>
        public IEnumerable<IClaim> Claims => _Claims;

        /// <inheritdoc/>
        public string AuthenticationType { get; }

        /// <inheritdoc/>
        public bool IsAuthenticated { get; }

        /// <inheritdoc/>
        public string Name => FindFirst(SanteDBClaimTypes.Name)?.Value;


        /// <inheritdoc/>
        public IEnumerable<IClaim> FindAll(string claimType) => _Claims.Where(c => c.Type == claimType);

        /// <inheritdoc/>
        public IClaim FindFirst(string claimType) => _Claims.FirstOrDefault(c => c.Type == claimType);
    }
}
