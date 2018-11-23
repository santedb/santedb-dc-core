/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 * 
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
 * Date: 2017-9-1
 */
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security
{
    /// <summary>
    /// Memory session manager service
    /// </summary>
    public class MemorySessionManagerService : ISessionManagerService
    {

        /// <summary>
        /// Sessions 
        /// </summary>
        private Dictionary<Guid, SessionInfo> m_session = new Dictionary<Guid, SessionInfo>();

        /// <summary>
        /// Authentication with the user and establish a session
        /// </summary>
        public SessionInfo Authenticate(string userName, string password, params Claim[] claims)
        {
            return this.Authenticate(userName, password, null, claims);
        }

        /// <summary>
        /// Authenticate the user and establish a sessions
        /// </summary>
        public SessionInfo Authenticate(string userName, string password, string tfaSecret, params Claim[] claims)
        {
            var idp = ApplicationContext.Current.GetService<IIdentityProviderService>();
            IPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(userName, false, claims));
            if (String.IsNullOrEmpty(tfaSecret))
                principal = idp.Authenticate(principal, password);
            else
                principal = idp.Authenticate(principal, password, tfaSecret);

            if (principal == null)
                throw new SecurityException(Strings.locale_sessionError);
            else
            {
                AuthenticationContext.Current = new AuthenticationContext(principal);
                var session = new SessionInfo(principal);
                session.Key = Guid.NewGuid();
                this.m_session.Add(session.Key.Value, session);
                return session;
            }
        }

        /// <summary>
        /// Authenticate the user with PIN
        /// </summary>
        public SessionInfo Authenticate(string userName, byte[] pin, params Claim[] claims)
        {
            var idp = ApplicationContext.Current.GetService<IPinAuthenticationService>();
            if (idp == null)
                throw new NotSupportedException("Authentication provide does not support PIN logins");
            else
            {
                var principal = idp.Authenticate(new ClaimsPrincipal(new ClaimsIdentity(userName, false, claims)), pin);
                if (principal == null)
                    throw new SecurityException(Strings.locale_sessionError);
                else
                {
                    var session = new SessionInfo(principal) { Key = Guid.NewGuid() };
                    this.m_session.Add(session.Key.Value, session);
                    return session;
                }

            }
        }

        /// <summary>
        /// Deletes the specified session
        /// </summary>
        public SessionInfo Delete(Guid sessionId)
        {
            SessionInfo ses = null;
            if (this.m_session.TryGetValue(sessionId, out ses))
                this.m_session.Remove(sessionId);
            return ses;
        }

        /// <summary>
        /// Get the specified session
        /// </summary>
        public SessionInfo Get(Guid sessionId)
        {
            SessionInfo ses = null;
            if (!this.m_session.TryGetValue(sessionId, out ses))
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
        /// Refreshes the specified session
        /// </summary>
        public SessionInfo Refresh(SessionInfo session, String password)
        {

            if (session == null) return session;
            var idp = ApplicationContext.Current.GetService<IIdentityProviderService>();

            // First is this a valid session?
            if (!this.m_session.ContainsKey(session.Key.Value))
                throw new KeyNotFoundException();

            var principal = idp.Authenticate(session.Principal, password);
            if (principal == null)
                throw new SecurityException(Strings.locale_sessionError);
            else
            {
                var newSession = new SessionInfo(principal);
                if (!this.m_session.ContainsKey(session.Key.Value))
                {
                    newSession.Key = Guid.NewGuid();
                    this.m_session.Add(newSession.Key.Value, newSession);
                }
                else
                {
                    newSession.Key = session.Key;
                    this.m_session[session.Key.Value] = newSession;
                }
                return session;
            }
        }
    }
}
