/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Core.Security
{
    /// <summary>
    /// Memory session manager service
    /// </summary>
    public class MemorySessionManagerService : ISessionManagerService, ISessionProviderService, ISessionIdentityProviderService
    {

        /// <summary>
        /// Sessions 
        /// </summary>
        private Dictionary<String, SessionInfo> m_session = new Dictionary<String, SessionInfo>();

        /// <summary>
        /// Get th service name
        /// </summary>
        public string ServiceName => "Memory based session manager";

        /// <summary>
        /// Establishment of session has been completed
        /// </summary>
        public event EventHandler<SessionEstablishedEventArgs> Established;

        /// <summary>
        /// Authentication with the user and establish a session
        /// </summary>
        public SessionInfo Authenticate(string userName, string password)
        {
            return this.Authenticate(userName, password, null);
        }

        /// <summary>
        /// Authenticate the user and establish a sessions
        /// </summary>
        public SessionInfo Authenticate(string userName, string password, string tfaSecret)
        {
            var idp = ApplicationContext.Current.GetService<IIdentityProviderService>();
            IPrincipal principal = null;
            if (String.IsNullOrEmpty(tfaSecret))
                principal = idp.Authenticate(userName, password);
            else
                principal = idp.Authenticate(userName, password, tfaSecret);

            if (principal == null)
                throw new SecurityException(Strings.locale_sessionError);
            else
            {
                return this.Establish(principal, DateTimeOffset.MaxValue, null) as SessionInfo;
            }
        }

        /// <summary>
        /// Authenticate the user with PIN
        /// </summary>
        public SessionInfo Authenticate(string userName, byte[] pin, params IClaim[] claims)
        {
            var idp = ApplicationContext.Current.GetService<IPinAuthenticationService>();
            if (idp == null)
                throw new NotSupportedException("Authentication provide does not support PIN logins");
            else
            {
                var principal = idp.Authenticate(userName, pin);
                if (principal == null)
                    throw new SecurityException(Strings.locale_sessionError);
                else
                    return this.Establish(principal, DateTimeOffset.MaxValue, null) as SessionInfo;

            }
        }

        /// <summary>
        /// Authenticate via session
        /// </summary>
        public IPrincipal Authenticate(ISession session)
        {
            return this.Get(Encoding.UTF8.GetString(session.Id, 0, session.Id.Length))?.Principal;
        }

        /// <summary>
        /// Deletes the specified session
        /// </summary>
        public SessionInfo Delete(IPrincipal principal)
        {
            SessionInfo ses = null;
            if (this.m_session.TryGetValue(principal.ToString(), out ses))
                this.m_session.Remove(principal.ToString());
            return ses;
        }

        /// <summary>
        /// Establish the session
        /// </summary>
        public ISession Establish(IPrincipal principal, DateTimeOffset expiry, string aud)
        {
            AuthenticationContext.Current = new AuthenticationContext(principal);
            try
            {
                var session = new SessionInfo(principal, null);
                session.Key = Guid.NewGuid();
                this.m_session.Add(session.Token, session);
                this.Established?.Invoke(this, new SessionEstablishedEventArgs(principal, session, true));
                return session;
            }
            catch
            {
                this.Established?.Invoke(this, new SessionEstablishedEventArgs(principal, null, true));
                throw;
            }
        }

        /// <summary>
        /// Extend the session
        /// </summary>
        public ISession Extend(byte[] refreshToken)
        {
            String tokenStr = Encoding.UTF8.GetString(refreshToken, 0, refreshToken.Length);
            return this.Refresh(this.m_session.FirstOrDefault(o => o.Value.RefreshToken == tokenStr).Value);
        }

        /// <summary>
        /// Get the specified session
        /// </summary>
        public SessionInfo Get(IPrincipal principal)
        {
            SessionInfo ses = null;
            if (!this.m_session.TryGetValue(principal.ToString(), out ses))
                return null;
            return ses;
        }

        /// <summary>
        /// Get session by the session token
        /// </summary>
        /// <param name="sessionToken">The session token</param>
        /// <returns>The session information</returns>
        public SessionInfo Get(String sessionToken)
        {
            return this.m_session.Values.FirstOrDefault(o => o.Token == sessionToken);
        }

        /// <summary>
        /// Get the specified session
        /// </summary>
        public ISession Get(byte[] sessionToken)
        {
            return this.Get(Encoding.UTF8.GetString(sessionToken, 0, sessionToken.Length));
        }

        /// <summary>
        /// Perform a refresh using the refresh token
        /// </summary>
        public SessionInfo Refresh(String refreshToken)
        {
            var session = this.m_session.Values.FirstOrDefault(o => o.RefreshToken == refreshToken && o.Expiry > DateTime.Now);
            if (session == null)
                throw new SecurityException(Strings.locale_session_expired);
            else
                return this.Refresh(session);
        }

        /// <summary>
        /// Refreshes the specified session
        /// </summary>
        public SessionInfo Refresh(SessionInfo session)
        {

            if (session == null) return session;
            var idp = ApplicationContext.Current.GetService<IIdentityProviderService>();

            // First is this a valid session?
            if (!this.m_session.ContainsKey(session.Token))
                throw new KeyNotFoundException();

            var principal = idp.ReAuthenticate(session.Principal);
            if (principal == null)
                throw new SecurityException(Strings.locale_sessionError);
            else
            {
                var newSession = new SessionInfo(principal, null);
                if (!this.m_session.ContainsKey(session.Token))
                {
                    this.m_session.Remove(session.Token);
                    newSession.Key = Guid.NewGuid();
                    this.m_session.Add(newSession.Token, newSession);

                }
                else
                {
                    newSession.Key = session.Key;
                    this.m_session[newSession.Token] = newSession;
                }
                return session;
            }
        }
    }
}
