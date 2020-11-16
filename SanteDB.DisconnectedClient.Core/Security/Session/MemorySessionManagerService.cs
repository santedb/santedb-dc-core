/*
 * Based on OpenIZ, Copyright (C) 2015 - 2020 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.Core;
using SanteDB.Core.Api.Security;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Security.Session
{
    /// <summary>
    /// Memory session manager service
    /// </summary>
    public class MemorySessionManagerService : ISessionProviderService, ISessionIdentityProviderService
    {

        /// <summary>
        /// Sessions 
        /// </summary>
        private Dictionary<String, MemorySession> m_session = new Dictionary<String, MemorySession>();

        // Security configuration section
        private SecurityConfigurationSection m_securityConfig = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SecurityConfigurationSection>();

        /// <summary>
        /// Get th service name
        /// </summary>
        public string ServiceName => "Memory based session manager";

        /// <summary>
        /// Establishment of session has been completed
        /// </summary>
        public event EventHandler<SessionEstablishedEventArgs> Established;

        /// <summary>
        /// Fired when a session is abandoned
        /// </summary>
        public event EventHandler<SessionEstablishedEventArgs> Abandoned;

        /// <summary>
        /// Gets the key for the specified session
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        private String GetSessionKey(byte[] sessionId)
        {
            return BitConverter.ToString(sessionId).Replace("-", "");
        }

        /// <summary>
        /// Authenticate via session
        /// </summary>
        public IPrincipal Authenticate(ISession session)
        {
            if (this.m_session.TryGetValue(this.GetSessionKey(session.Id), out MemorySession memSession))
                return memSession.Principal;
            return null;
        }

        /// <summary>
        /// Abandon the specified session
        /// </summary>
        /// <param name="session">The session to be abandoned</param>
        public void Abandon(ISession session)
        {
            if (this.m_session.TryGetValue(this.GetSessionKey(session.Id), out MemorySession ses))
            {
                this.m_session.Remove(this.GetSessionKey(session.Id));
                this.Abandoned?.Invoke(this, new SessionEstablishedEventArgs(ses.Principal, ses, true, false, null, null));
            }
            else
                this.Abandoned?.Invoke(this, new SessionEstablishedEventArgs(null, null, false, false, null, null));
        }

        /// <summary>
        /// Establish the session
        /// </summary>
        public ISession Establish(IPrincipal principal, string aud, bool isOverride, string purpose, string[] policyDemands, string language)
        {
            AuthenticationContext.Current = new AuthenticationContext(principal);
            try
            {
                // Setup claims
                var cprincipal = principal as IClaimsPrincipal;
                var claims = cprincipal.Claims.ToList();

                // Did the caller explicitly set policies?
                var pip = ApplicationServiceContext.Current.GetService<IPolicyInformationService>();

                // Is the principal only valid for pwd reset?
                if (!isOverride && cprincipal.HasClaim(o => o.Type == SanteDBClaimTypes.SanteDBScopeClaim)) // Allow the createor to specify
                    ;
                else if (policyDemands?.Length > 0)
                {

                    if (isOverride)
                        claims.Add(new SanteDBClaim(SanteDBClaimTypes.SanteDBOverrideClaim, "true"));
                    if (!String.IsNullOrEmpty(purpose))
                        claims.Add(new SanteDBClaim(SanteDBClaimTypes.PurposeOfUse, purpose));

                    var pdp = ApplicationServiceContext.Current.GetService<IPolicyDecisionService>();
                    foreach (var pol in policyDemands)
                    {
                        // Get grant
                        var grant = pdp.GetPolicyOutcome(cprincipal, pol);
                        if (isOverride && grant == PolicyGrantType.Elevate &&
                            (pol.StartsWith(PermissionPolicyIdentifiers.SecurityElevations) || // Special security elevations don't require override permission
                            pdp.GetPolicyOutcome(cprincipal, PermissionPolicyIdentifiers.OverridePolicyPermission) == PolicyGrantType.Grant
                            )) // We are attempting to override
                            claims.Add(new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, pol));
                        else if (grant == PolicyGrantType.Grant)
                            claims.Add(new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, pol));
                        else
                            throw new PolicyViolationException(cprincipal, pol, grant);
                    }
                }


                // Add default policy claims
                if (pip != null)
                {
                    List<IPolicyInstance> oizPrincipalPolicies = new List<IPolicyInstance>();
                    foreach (var pol in pip.GetActivePolicies(cprincipal).GroupBy(o => o.Policy.Oid))
                        oizPrincipalPolicies.Add(pol.FirstOrDefault(o => (int)o.Rule == pol.Min(r => (int)r.Rule)));
                    // Scopes user is allowed to access
                    claims.AddRange(oizPrincipalPolicies.Where(o => o.Rule == PolicyGrantType.Grant).Select(o => new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, o.Policy.Oid)));
                }

                var sessionKey = Guid.NewGuid();
                var sessionRefresh = Guid.NewGuid();
                claims.Add(new SanteDBClaim(SanteDBClaimTypes.SanteDBSessionIdClaim, sessionKey.ToString()));

                if (!String.IsNullOrEmpty(language))
                    claims.Add(new SanteDBClaim(SanteDBClaimTypes.Language, language));

                // Is the principal a remote principal (i.e. not issued by us?)
                MemorySession memorySession = null;
                if (principal is IOfflinePrincipal)
                    memorySession = new MemorySession(sessionKey, DateTime.Now, DateTime.Now.Add(this.m_securityConfig.MaxLocalSession), sessionRefresh.ToByteArray(), claims.ToArray(), principal);
                else
                {
                    DateTime notAfter = claims.FirstOrDefault(o => o.Type == SanteDBClaimTypes.Expiration).AsDateTime(),
                        notBefore = claims.FirstOrDefault(o => o.Type == SanteDBClaimTypes.AuthenticationInstant).AsDateTime();
                    memorySession = new MemorySession(sessionKey, notBefore, notAfter, sessionRefresh.ToByteArray(), claims.ToArray(), principal);
                }

                this.m_session.Add(this.GetSessionKey(sessionKey.ToByteArray()), memorySession);
                this.Established?.Invoke(this, new SessionEstablishedEventArgs(principal, memorySession, true, isOverride, purpose, policyDemands));
                return memorySession;
            }
            catch(Exception e)
            {
                this.Established?.Invoke(this, new SessionEstablishedEventArgs(principal, null, false, isOverride, purpose, policyDemands));
                throw new Exception($"Error establishing session for {principal.Identity.Name}", e);
            }
        }

        /// <summary>
        /// Extend the session
        /// </summary>
        public ISession Extend(byte[] refreshToken)
        {
            var tokenKey = this.GetSessionKey(refreshToken);
            var session = this.m_session.FirstOrDefault(o => o.Value.RefreshTokenString == tokenKey).Value;

            if (session.Claims.Any(r => r.Type == SanteDBClaimTypes.SanteDBOverrideClaim))
                throw new InvalidOperationException("Cannot extend/refresh an override claim");

            // Refresh the principal
            var principal = ApplicationServiceContext.Current.GetService<IIdentityProviderService>().ReAuthenticate(session.Principal);

            // First, we want to abandon the session
            this.Abandon(session);
            session = this.Establish(principal,
                null,
                false,
                null,
                null,
                session.Claims.FirstOrDefault(o => o.Type == SanteDBClaimTypes.Language)?.Value) as MemorySession;

            return session;
        }

        /// <summary>
        /// Get the specified token
        /// </summary>
        public ISession Get(byte[] sessionToken, bool allowExpired = false)
        {
            if (this.m_session.TryGetValue(this.GetSessionKey(sessionToken), out MemorySession ses) && (allowExpired ^ (ses.NotAfter > DateTime.Now)))
                return ses;
            return null;
        }

        /// <summary>
        /// Get identities related to the session
        /// </summary>
        public IIdentity[] GetIdentities(ISession session)
        {
            if (session is MemorySession memorySession)
            {
                if (memorySession.Principal is IClaimsPrincipal cprincipal)
                    return cprincipal.Identities;
                else
                    return new IIdentity[] { memorySession.Principal.Identity };
            }
            return null;
        }
    }
}
