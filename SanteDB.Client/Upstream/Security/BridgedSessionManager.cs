/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Caching.Memory.Session;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// Represents a bridged session provider manager
    /// </summary>
    /// <remarks>
    /// This class is responsible for managing local sessions (via a synchronized pattern) as well as upstream sessions which need to 
    /// interact with the upstream, as well as transitioning between the two.
    /// </remarks>
    [PreferredService(typeof(ISessionProviderService))]
    [PreferredService(typeof(ISessionIdentityProviderService))]
    public class BridgedSessionManager : ISessionProviderService, ISessionIdentityProviderService
    {

        /// <summary>
        /// Session comparer
        /// </summary>
        private class SessionComparer : IEqualityComparer<ISession>
        {
            /// <inheritdoc/>
            public bool Equals(ISession x, ISession y)
            {
                return x.Id.SequenceEqual(y.Id) || x.Claims.Any(ox => y.Claims.Any(oy => ox.Type == SanteDBClaimTypes.SecurityId && oy.Type == ox.Type && ox.Value == oy.Type));
            }

            public int GetHashCode(ISession obj)
            {
                throw new NotImplementedException();
            }
        }

        private const string LOCAL_SESSION_TYPE = "LOCAL";
        private const string REMOTE_SESSION_TYPE = "MEMORY";

        private readonly MemorySessionManagerService m_memorySessionManager;
        private readonly ISessionProviderService m_localSessionManager;
        private readonly ISessionIdentityProviderService m_localSessionIdentityManager;

        /// <summary>
        /// DI CTOR
        /// </summary>
        public BridgedSessionManager(IServiceManager serviceManager,
            ILocalServiceProvider<ISessionProviderService> localSessionManager,
            ILocalServiceProvider<ISessionIdentityProviderService> localSessionIdentityProvider)
        {
            this.m_memorySessionManager = serviceManager.CreateInjected<MemorySessionManagerService>();
            this.m_localSessionManager = localSessionManager.LocalProvider;
            this.m_localSessionIdentityManager = localSessionIdentityProvider.LocalProvider;
            // Propogate the events
            this.m_memorySessionManager.Abandoned += (o, e) => this.Abandoned?.Invoke(o, e);
            this.m_memorySessionManager.Established += (o, e) => this.Established?.Invoke(o, e);
            this.m_memorySessionManager.Extended += (o, e) => this.Extended?.Invoke(o, e);
            this.m_localSessionManager.Abandoned += (o, e) => this.Abandoned?.Invoke(o, e);
            this.m_localSessionManager.Established += (o, e) => this.Established?.Invoke(o, e);
            this.m_localSessionManager.Extended += (o, e) => this.Extended?.Invoke(o, e);
        }

        /// <inheritdoc/>
        public string ServiceName => "Bridged Session Manager";

        /// <inheritdoc/>
        public event EventHandler<SessionEstablishedEventArgs> Established;
        /// <inheritdoc/>
        public event EventHandler<SessionEstablishedEventArgs> Abandoned;
        /// <inheritdoc/>
        public event EventHandler<SessionEstablishedEventArgs> Extended;

        /// <inheritdoc/>
        public void Abandon(ISession session)
        {
            // Abandon the session on both
            this.m_memorySessionManager.Abandon(session);
            this.m_localSessionManager.Abandon(session);
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(ISession session)
        {
            try
            {
                return this.m_memorySessionManager.Authenticate(session);
            }
            catch
            {
                return this.m_localSessionIdentityManager.Authenticate(session);
            }
        }

        /// <inheritdoc/>
        public ISession Establish(IPrincipal principal, string remoteEp, bool isOverride, string purpose, string[] scope, string lang)
        {
            if (principal is ITokenPrincipal tokenPrinicpal) // Principal came from a token source - upstream
            {
                return this.m_memorySessionManager.Establish(principal, remoteEp, isOverride, purpose, scope, lang);
            }
            else
            {
                return this.m_localSessionManager.Establish(principal, remoteEp, isOverride, purpose, scope, lang);
            }
        }

        /// <inheritdoc/>
        public ISession Extend(byte[] refreshToken)
        {
            try
            {
                return this.m_memorySessionManager.Extend(refreshToken);
            }
            catch (KeyNotFoundException) // Session is not registered with the remote one
            {
                return this.m_localSessionManager.Extend(refreshToken);
            }
        }

        /// <inheritdoc/>
        public ISession Get(byte[] sessionId, bool allowExpired = false)
        {
            return this.m_memorySessionManager.Get(sessionId, allowExpired) ?? this.m_localSessionManager.Get(sessionId, allowExpired);
        }

        /// <inheritdoc/>
        public ISession[] GetActiveSessions()
        {
            return this.m_memorySessionManager.GetActiveSessions().Union(this.m_localSessionManager.GetActiveSessions()).Distinct(new SessionComparer()).ToArray();
        }

        /// <inheritdoc/>
        public IIdentity[] GetIdentities(ISession session)
        {
            try
            {
                return this.m_memorySessionManager.GetIdentities(session) ?? this.m_localSessionIdentityManager.GetIdentities(session);
            }
            catch
            {
                return this.m_localSessionIdentityManager.GetIdentities(session);
            }
        }

        /// <inheritdoc/>
        public ISession[] GetUserSessions(Guid userKey)
        {
            return this.m_memorySessionManager.GetUserSessions(userKey).Union(this.m_localSessionManager.GetUserSessions(userKey)).Distinct(new SessionComparer()).ToArray();
        }
    }
}
