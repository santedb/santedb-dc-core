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
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// Represents an application identity created from an OAUTH token
    /// </summary>
    internal class OAuthApplicationIdentity : IApplicationIdentity, IClaimsIdentity
    {

        // App SID
        private readonly Guid m_applicationSid;

        /// <summary>
        /// Create a new oauth application identity
        /// </summary>
        public OAuthApplicationIdentity(IEnumerable<IClaim> claims)
        {
            // Audience of the claim is the application name
            this.Name = claims.FirstOrDefault(o => o.Type == SanteDBClaimTypes.AudienceClaim)?.Value ?? claims.First(o => o.Type == SanteDBClaimTypes.SanteDBApplicationNameClaim).Value;
            this.m_applicationSid = Guid.Parse(claims.First(o => o.Type == SanteDBClaimTypes.SanteDBApplicationIdentifierClaim).Value);
        }

        /// <inheritdoc/>
        public string AuthenticationType => "OAUTH";

        /// <inheritdoc/>
        public bool IsAuthenticated => false;

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public IEnumerable<IClaim> Claims => new IClaim[]
        {
            new SanteDBClaim(SanteDBClaimTypes.SecurityId, this.m_applicationSid.ToString()),
            new SanteDBClaim(SanteDBClaimTypes.SanteDBApplicationIdentifierClaim, this.m_applicationSid.ToString()),
            new SanteDBClaim(SanteDBClaimTypes.Actor, ActorTypeKeys.Application.ToString()),
            new SanteDBClaim(SanteDBClaimTypes.Name, this.Name)
        };

        /// <inheritdoc/>
        public IEnumerable<IClaim> FindAll(string claimType) => this.Claims.Where(t => t.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase));

        /// <inheritdoc/>
        public IClaim FindFirst(string claimType) => this.Claims.FirstOrDefault(t => t.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase));
    }
}