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
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// Represents an identity provider which bridges local and upstream 
    /// </summary>
    [PreferredService(typeof(IIdentityProviderService))]
    [PreferredService(typeof(ISecurityChallengeIdentityService))]
    public class BridgedIdentityProvider : UpstreamServiceBase, IIdentityProviderService, ISecurityChallengeIdentityService
    {

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(BridgedIdentityProvider));
        private readonly object m_lockObject = new object();
        private IIdentityProviderService m_localIdentityProvider;
        private readonly IRoleProviderService m_localRoleProvider;
        private readonly IPolicyInformationService m_localPip;
        private readonly ISecurityChallengeIdentityService m_localChallenge;
        private readonly ILocalizationService m_localizationService;
        private readonly IIdentityProviderService m_upstreamIdentityProvider;
        private readonly IRoleProviderService m_upstreamRoleProvider;
        private readonly IPolicyInformationService m_upstreamPip;
        private readonly ISecurityChallengeIdentityService m_upstreamChallenge;
        private readonly ISecurityRepositoryService m_upstreamSecurityRepository;
        private readonly IThreadPoolService m_threadPoolService;
        private readonly IDataPersistenceService<UserEntity> m_localUserEntityRepository;

        /// <summary>
        /// Bridged identity provider
        /// </summary>
        public string ServiceName => "Bridged Identity Provider";

        /// <summary>
        /// Authenticating
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        /// <summary>
        /// Authenticated
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// DI constructor
        /// </summary>
        public BridgedIdentityProvider(
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamServiceProvider<IIdentityProviderService> upstreamIdentityProvider,
            ILocalServiceProvider<IIdentityProviderService> localIdentityProvider,
            IUpstreamServiceProvider<IRoleProviderService> upstreamRoleProvider,
            ILocalServiceProvider<IRoleProviderService> localRoleProvider,
            IUpstreamServiceProvider<IPolicyInformationService> upstreamPip,
            ILocalServiceProvider<IPolicyInformationService> localPip,
            IUpstreamServiceProvider<ISecurityChallengeIdentityService> upstreamSecurityChallenge,
            IThreadPoolService threadPoolService,
            ILocalServiceProvider<ISecurityChallengeIdentityService> localSecurityChallenge,
            IUpstreamServiceProvider<ISecurityRepositoryService> upstreamSecurityRepository,
            IDataPersistenceService<UserEntity> localUserEntityRepositoryService,
            ILocalizationService localizationService
            ) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider)
        {
            this.m_threadPoolService = threadPoolService;
            this.m_localUserEntityRepository = localUserEntityRepositoryService;
            this.m_upstreamSecurityRepository = upstreamSecurityRepository.UpstreamProvider;
            this.m_upstreamIdentityProvider = upstreamIdentityProvider.UpstreamProvider;
            this.m_upstreamRoleProvider = upstreamRoleProvider.UpstreamProvider;
            this.m_upstreamPip = upstreamPip.UpstreamProvider;
            this.m_upstreamChallenge = upstreamSecurityChallenge.UpstreamProvider;
            this.m_localIdentityProvider = localIdentityProvider.LocalProvider;
            this.m_localRoleProvider = localRoleProvider.LocalProvider;
            this.m_localPip = localPip.LocalProvider;
            this.m_localChallenge = localSecurityChallenge.LocalProvider;
            this.m_localizationService = localizationService;
        }


        /// <summary>
        /// Returns true if <paramref name="userName"/> is a local only identity
        /// </summary>
        private bool IsLocalIdentity(string userName)
        {
            return this.m_localIdentityProvider.GetClaims(userName)?.Any(c => c.Type == SanteDBClaimTypes.LocalOnly) == true;
        }

        /// <summary>
        /// True if remote authentication should be performed
        /// </summary>
        private bool ShouldDoRemoteAuthentication(string userName)
        {
            if (!base.IsUpstreamConfigured)
            {
                return true;
            }

            // local user
            return !this.IsLocalIdentity(userName) &&
                base.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AuthenticationService);
        }

        /// <summary>
        /// Syncrhonize the <paramref name="remoteIdentity"/> to the local provider
        /// </summary>
        /// <param name="remoteIdentity">The remote identity to be synchronized</param>
        /// <param name="password">The password used to successfully authenticate against the upstream</param>
        private void SynchronizeIdentity(IClaimsPrincipal remoteIdentity, string password)
        {
            try
            {
                using (AuthenticationContext.EnterSystemContext())
                {
                    if (remoteIdentity == null)
                    {
                        throw new ArgumentNullException(nameof(remoteIdentity));
                    }

                    // This is an IIdentityProvider which the ISecurityRepository service relies on - we want to only call it from the service context
                    var upstreamSecurityRepository = ApplicationServiceContext.Current.GetService<IUpstreamServiceProvider<ISecurityRepositoryService>>()?.UpstreamProvider;
                    var localSecurityRepository = ApplicationServiceContext.Current.GetService<ILocalServiceProvider<ISecurityRepositoryService>>()?.LocalProvider;
                    var localUserRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityUser>>();

                    // Get the user identity 
                    var userIdentity = remoteIdentity.Identities.FirstOrDefault(o => o.FindFirst(SanteDBClaimTypes.Actor).Value == ActorTypeKeys.HumanUser.ToString());
                    if (userIdentity == null)
                    {
                        throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(SynchronizeIdentity)));
                    }

                    // Create local identity 
                    var localUser = localUserRepository.Find(o => o.UserName.ToLowerInvariant() == userIdentity.Name.ToLowerInvariant() && o.ObsoletionTime == null).FirstOrDefault();
                    var upstreamUserInfo = upstreamSecurityRepository.GetUser(userIdentity);
                    if (localUser == null) // create the user with the same SID
                    {
                        upstreamUserInfo.Password = password;
                        localUser = localUserRepository.Insert(upstreamUserInfo);
                    }
                    else
                    {
                        localUser = localUserRepository.Save(upstreamUserInfo);
                    }

                    if (!String.IsNullOrEmpty(password))
                    {
                        this.m_localIdentityProvider.ChangePassword(userIdentity.Name, password, remoteIdentity, true);
                    }

                    // Synchronize the roles that this user belongs to 
                    var localRoles = this.m_localRoleProvider.GetAllRoles();

                    // Remove all roles for the local user
                    this.m_localRoleProvider.RemoveUsersFromRoles(new string[] { userIdentity.Name }, localRoles, AuthenticationContext.SystemPrincipal);
                    var upstreamUserRoles = userIdentity.FindAll(SanteDBClaimTypes.DefaultRoleClaimType).Select(o => o.Value).ToArray();

                    // We want to prevent there from being multiple users with multiple roles all hitting the role provider
                    lock (this.m_lockObject)
                    {
                        foreach (var roleName in upstreamUserRoles)
                        {
                            if (!localRoles.Contains(roleName))
                            {
                                this.m_tracer.TraceInfo("Creating remote role - {0}", roleName);
                                this.m_localRoleProvider.CreateRole(roleName, AuthenticationContext.SystemPrincipal);
                            }

                            var role = localSecurityRepository.GetRole(roleName);
                            // Synchronize the most accurate policies for the role 
                            var upstreamPolicies = this.m_upstreamPip.GetPolicies(role);
                            // Clear policies for role
                            this.m_localPip.RemovePolicies(role, AuthenticationContext.SystemPrincipal, this.m_localPip.GetPolicies(role).Select(o => o.Policy.Oid).ToArray());
                            // Add 
                            foreach (var pol in upstreamPolicies.GroupBy(o => o.Rule))
                            {
                                this.m_localPip.AddPolicies(role, pol.Key, AuthenticationContext.SystemPrincipal, pol.Select(o => o.Policy.Oid).ToArray());
                            }

                        }
                    }

                    // Add user to roles
                    this.m_localRoleProvider.AddUsersToRoles(new string[] { userIdentity.Name }, upstreamUserRoles, AuthenticationContext.SystemPrincipal);

                    // Upstream user profile
                    var userProfile = this.m_upstreamSecurityRepository.GetUserEntity(remoteIdentity.Identity);
                    if (userProfile != null)
                    {
                        if (this.m_localUserEntityRepository.Get(userProfile.Key.Value, null, AuthenticationContext.SystemPrincipal) == null)
                        {
                            this.m_localUserEntityRepository.Insert(userProfile, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                        }
                        else
                        {
                            this.m_localUserEntityRepository.Update(userProfile, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                this.m_tracer.TraceWarning("Cannot synchronize identity - {0}", ex);
            }

        }

        /// <inheritdoc/>
        public IIdentity GetIdentity(string userName)
        {
            if (this.IsLocalIdentity(userName) || !this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                return this.m_localIdentityProvider.GetIdentity(userName);
            }
            else
            {
                return this.m_upstreamIdentityProvider.GetIdentity(userName);
            }
        }

        /// <inheritdoc/>
        public IIdentity GetIdentity(Guid sid)
        {
            return this.m_localIdentityProvider.GetIdentity(sid) ??
                this.m_upstreamIdentityProvider.GetIdentity(sid);
        }

        /// <inheritdoc/>
        public IIdentity CreateIdentity(string userName, string password, IPrincipal principal, Guid? withSid = null)
        {

            var localIdentity = this.m_localIdentityProvider.CreateIdentity(userName, password, principal, withSid);

            if (!withSid.HasValue) // This is a new user
            {
                this.m_localIdentityProvider.AddClaim(userName, new SanteDBClaim(SanteDBClaimTypes.LocalOnly, "true"), principal);
            }
            return this.m_localIdentityProvider.GetIdentity(userName);
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string userName, string password, IEnumerable<IClaim> clientClaimAssertions = null, IEnumerable<String> demandedScopes = null) => this.Authenticate(userName, password, null, clientClaimAssertions, demandedScopes);


        /// <inheritdoc/>
        public IPrincipal Authenticate(string userName, string password, string tfaSecret, IEnumerable<IClaim> clientClaimAssertions = null, IEnumerable<String> demandedScopes = null)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName));
            }

            var authenticatingArgs = new AuthenticatingEventArgs(userName);
            this.Authenticating?.Invoke(this, authenticatingArgs);
            if (authenticatingArgs.Cancel)
            {
                this.m_tracer.TraceVerbose("Authenticating Event signals cancel.");
                if (authenticatingArgs.Success)
                {
                    return authenticatingArgs.Principal;
                }
                else
                {
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_CANCELLED));
                }
            }

            IPrincipal result = null;
            try
            {
                if (this.ShouldDoRemoteAuthentication(userName))
                {
                    try
                    {
                        result = this.m_upstreamIdentityProvider.Authenticate(userName, password, tfaSecret, clientClaimAssertions, demandedScopes);
                        this.m_threadPoolService.QueueUserWorkItem((o) => this.SynchronizeIdentity(o.result as IClaimsPrincipal, o.password), new { result = result, password = password });
                    }
                    catch (RestClientException<Object> e)
                    {
                        throw e;
                    }
                    catch (UpstreamIntegrationException e) when (e.InnerException is TimeoutException)
                    {
                        result = this.m_localIdentityProvider.Authenticate(userName, password, tfaSecret, clientClaimAssertions);
                    }
                    catch (TimeoutException)
                    {
                        result = this.m_localIdentityProvider.Authenticate(userName, password, tfaSecret, clientClaimAssertions);
                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
                    }
                }
                else
                {
                    result = String.IsNullOrEmpty(tfaSecret) ?
                        this.m_localIdentityProvider.Authenticate(userName, password, clientClaimAssertions) :
                        this.m_localIdentityProvider.Authenticate(userName, password, tfaSecret, clientClaimAssertions);
                }
            }
            finally
            {
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, result, result != null));
            }
            return result;
        }

        /// <inheritdoc/>
        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal is ITokenPrincipal) // It is upstream so we have to do upstream
            {
                var result = this.m_upstreamIdentityProvider.ReAuthenticate(principal);
                this.m_threadPoolService.QueueUserWorkItem(o => this.SynchronizeIdentity(o.result as IClaimsPrincipal, null), new { result = result });
                return result;
            }
            else
            {
                return this.m_localIdentityProvider.ReAuthenticate(principal);
            }
        }

        /// <inheritdoc/>
        public void ChangePassword(string userName, string newPassword, IPrincipal principal, bool force = false)
        {
            // Changing secret on a bridged user only applies if the user has been logged in
            if (this.m_localIdentityProvider.GetIdentity(userName) == null)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(ChangePassword)));
            }

            if (!this.IsLocalIdentity(userName))
            {
                this.m_upstreamIdentityProvider.ChangePassword(userName, newPassword, principal);
            }
            // Change locally 
            this.m_localIdentityProvider.ChangePassword(userName, newPassword, principal);
        }

        /// <inheritdoc/>
        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            this.m_localIdentityProvider.DeleteIdentity(userName, principal);
            if (!this.IsLocalIdentity(userName))
            {
                this.m_tracer.TraceWarning("Identity {0} only deleted on loca device", userName);
            }
        }

        /// <inheritdoc/>
        public void SetLockout(string userName, bool lockout, IPrincipal principal)
        {
            this.m_localIdentityProvider.SetLockout(userName, lockout, principal);
            if (!this.IsLocalIdentity(userName))
            {
                this.m_tracer.TraceWarning("Identity {0} only lockout on local device", userName);
            }
        }

        /// <inheritdoc/>
        public void AddClaim(string userName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            this.m_localIdentityProvider.AddClaim(userName, claim, principal, expiry);
            if (!this.IsLocalIdentity(userName))
            {
                this.m_tracer.TraceWarning("Claim on identity {0} only applies to local device", userName);
            }
        }

        /// <inheritdoc/>
        public void RemoveClaim(string userName, string claimType, IPrincipal principal)
        {
            this.m_localIdentityProvider.RemoveClaim(userName, claimType, principal);
            if (!this.IsLocalIdentity(userName))
            {
                this.m_tracer.TraceWarning("Claim on identity {0} only applies to local device", userName);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IClaim> GetClaims(string userName) => this.m_localIdentityProvider.GetClaims(userName);

        /// <inheritdoc/>
        public Guid GetSid(string userName)
        {
            if (this.ShouldDoRemoteAuthentication(userName))
            {
                return this.m_upstreamIdentityProvider.GetSid(userName);
            }
            else
            {
                return this.m_localIdentityProvider.GetSid(userName);
            }
        }

        /// <inheritdoc/>
        public AuthenticationMethod GetAuthenticationMethods(string userName)
        {
            if (this.IsLocalIdentity(userName))
            {
                return AuthenticationMethod.Local;
            }
            else
            {
                return AuthenticationMethod.Any;
            }
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string userName, Guid challengeKey, string response, string tfaSecret)
        {
            if (this.ShouldDoRemoteAuthentication(userName))
            {
                return this.m_upstreamChallenge.Authenticate(userName, challengeKey, response, tfaSecret);
            }
            else
            {
                return this.m_localChallenge.Authenticate(userName, challengeKey, response, tfaSecret);
            }
        }

        /// <inheritdoc/>
        public void ExpirePassword(string userName, IPrincipal principal)
        {
            this.m_localIdentityProvider.ExpirePassword(userName, principal);
            if (!this.IsLocalIdentity(userName))
            {
                this.m_tracer.TraceWarning("Identity {0} only lockout on local device", userName);
            }
        }
    }
}
