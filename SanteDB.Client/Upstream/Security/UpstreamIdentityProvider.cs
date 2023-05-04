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
using SanteDB.Client.Exceptions;
using SanteDB.Client.OAuth;
using SanteDB.Client.Repositories;
using SanteDB.Client.Services;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Rest.AMI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// Represents an implementation of the <see cref="IIdentityProviderService"/> which uses an upstream oauth server
    /// </summary>
    [PreferredService(typeof(IIdentityProviderService))]
    [PreferredService(typeof(ISecurityChallengeIdentityService))]
    public class UpstreamIdentityProvider : UpstreamServiceBase, IIdentityProviderService, ISecurityChallengeIdentityService
    {
        readonly ILocalServiceProvider<IIdentityProviderService> _LocalIdentityProvider;
        readonly IOAuthClient _OAuthClient;
        private readonly ILocalServiceProvider<ISecurityChallengeIdentityService> _LocalSecurityChallengeService;
        readonly ILocalizationService _LocalizationService;
        readonly IUpstreamServiceProvider<IPolicyInformationService> _RemotePolicyInformationService;
        readonly ILocalServiceProvider<IPolicyInformationService> _LocalPolicyInformationService;
        readonly IRoleProviderService _RemoteRoleProviderService;
        readonly ILocalServiceProvider<IRoleProviderService> _LocalRoleProviderService;
        //readonly ISecurityRepositoryService _SecurityRepositoryService;

        readonly bool _CanSyncPolicies;
        readonly bool _CanSyncRoles;

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamIdentityProvider(
            IOAuthClient oauthClient,
            IRestClientFactory restClientFactory,
            ILocalizationService localizationService,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamServiceProvider<IPolicyInformationService> remotePolicyInformationService,
            IRoleProviderService remoteRoleProviderService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            ILocalServiceProvider<ISecurityChallengeIdentityService> localSecurityChallengeService = null,
            ILocalServiceProvider<IPolicyInformationService> localPolicyInformationService = null,
            ILocalServiceProvider<IRoleProviderService> localRoleProviderService = null,
            ILocalServiceProvider<IIdentityProviderService> localIdentityProvider = null) // on initial configuration in online only mode there are no local users
            : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider)
        {
            _LocalIdentityProvider = localIdentityProvider;
            _LocalizationService = localizationService;
            _RemotePolicyInformationService = remotePolicyInformationService;
            _RemoteRoleProviderService = remoteRoleProviderService;
            _LocalRoleProviderService = localRoleProviderService;
            //_SecurityRepositoryService = securityRepositoryService;
            _LocalPolicyInformationService = localPolicyInformationService;
            _OAuthClient = oauthClient ?? throw new ArgumentNullException(nameof(oauthClient));
            _LocalSecurityChallengeService = localSecurityChallengeService;
            _CanSyncPolicies = null != _LocalPolicyInformationService && _RemotePolicyInformationService != _LocalPolicyInformationService;
            _CanSyncRoles = null != _LocalRoleProviderService && _RemoteRoleProviderService != _LocalRoleProviderService;
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Identity Provider";

        /// <inheritdoc/>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        /// <inheritdoc/>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// Determines if <paramref name="userName"/> requires upstream authentication
        /// </summary>
        /// <param name="userName">The name of the user to check</param>
        /// <returns>True if upstream authentication is required, false if local authentication can be used</returns>
        private bool ShouldDoRemoteAuthentication(string userName)
        {
            //Null local provider so we're in online-only or initial mode.
            if (null == _LocalIdentityProvider || !IsUpstreamConfigured)
            {
                return true;
            }

            //Try to check if the local user is marked as local only.
            var localuser = _LocalIdentityProvider.LocalProvider.GetIdentity(userName);

            if (localuser is IClaimsIdentity icl)
            {
                if (icl.Claims.FirstOrDefault(c => c.Type == SanteDBClaimTypes.LocalOnly) != null)
                {
                    return false;
                }
            }

            return IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AuthenticationService);
        }

        /// <summary>
        /// Synchronize the remote <paramref name="remoteIdentity"/> with the successfully used <paramref name="password"/>
        /// to the local cache
        /// </summary>
        /// <param name="password">The password used to successfully establish <paramref name="remoteIdentity"/></param>
        /// <param name="remoteIdentity">The remote identity to be synchronized</param>
        /// <remarks>Used to cache the most recent security credentials for offline authentication</remarks>
        protected void SynchronizeLocalIdentity(IClaimsPrincipal remoteIdentity, string password)
        {
            if (null == remoteIdentity)
            {
                throw new ArgumentNullException(nameof(remoteIdentity), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (null == _LocalIdentityProvider) //If there is no local identity provider, then we don't need to do anything.
            {
                return;
            }

            var useridentity = remoteIdentity.Identity as IClaimsIdentity; //Coming from OAuth, there will always be one that represents the user.

            var policiestosync = new List<string>();
            var rolestosync = new List<string>();
            string userid = null;

            policiestosync.AddRange(remoteIdentity.Claims.Where(c => c.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(c => c.Value));
            rolestosync.AddRange(remoteIdentity.Claims.Where(c => c.Type == SanteDBClaimTypes.DefaultRoleClaimType).Select(c => c.Value));

            if (null != useridentity)
            {
                policiestosync.AddRange(useridentity.Claims.Where(c => c.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(c => c.Value));
                rolestosync.AddRange(useridentity.Claims.Where(c => c.Type == SanteDBClaimTypes.DefaultRoleClaimType).Select(c => c.Value));

                userid = useridentity.GetFirstClaimValue(SanteDBClaimTypes.SanteDBUserIdentifierClaim, SanteDBClaimTypes.SecurityId);
            }

            var localuser = _LocalIdentityProvider.LocalProvider.GetIdentity(useridentity.Name);

            if (null == localuser)
            {
                localuser = _LocalIdentityProvider.LocalProvider.CreateIdentity(useridentity.Name, password, AuthenticationContext.SystemPrincipal);
            }
            else
            {
                _LocalIdentityProvider.LocalProvider.ChangePassword(useridentity.Name, password, remoteIdentity, force: true);
            }

            if (_CanSyncPolicies)
            {
                foreach (var policyoid in policiestosync)
                {
                    var localpolicy = _LocalPolicyInformationService.LocalProvider.GetPolicy(policyoid);

                    if (null == localpolicy)
                    {
                        var remotepolicy = _RemotePolicyInformationService.UpstreamProvider.GetPolicy(policyoid);
                        _LocalPolicyInformationService.LocalProvider.CreatePolicy(remotepolicy, AuthenticationContext.SystemPrincipal);
                    }
                }
            }

            if (_CanSyncRoles)
            {
                var localroles = _LocalRoleProviderService.LocalProvider.GetAllRoles();
                foreach (var rolename in rolestosync)
                {
                    if (!localroles.Contains(rolename))
                    {
                        _LocalRoleProviderService.LocalProvider.CreateRole(rolename, AuthenticationContext.SystemPrincipal);
                    }

                    var role = new SecurityRole { Name = rolename }; //TODO: Current implementations will not allow this to work locally.

                    var rolepolicies = _RemotePolicyInformationService.UpstreamProvider.GetPolicies(role);

                    foreach (var policy in rolepolicies)
                    {
                        if (null == _LocalPolicyInformationService.LocalProvider.GetPolicy(policy.Policy.Oid))
                        {
                            _LocalPolicyInformationService.LocalProvider.CreatePolicy(policy.Policy, AuthenticationContext.SystemPrincipal);
                        }

                        _LocalPolicyInformationService.LocalProvider.AddPolicies(role, policy.Rule, AuthenticationContext.SystemPrincipal, policy.Policy.Oid);
                    }
                }
            }
        }


        private void ChangeRemotePassword(string username, string newPassword)
        {
            using (var amiclient = CreateAmiServiceClient())
            {
                var remoteuser = amiclient.GetUsers(o => o.UserName.ToLowerInvariant() == username.ToLowerInvariant()).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault()?.Entity;

                if (null == remoteuser?.Key)
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(SecurityUser) }));
                }

                remoteuser.Password = newPassword;

                try
                {
                    var result = amiclient.UpdateUser(remoteuser.Key.Value, new Core.Model.AMI.Auth.SecurityUserInfo(remoteuser) { PasswordOnly = true });

                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = remoteuser.Key.Value }), ex);
                }
            }
        }

        /// <inheritdoc/>
        public void AddClaim(string userName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            if (_LocalIdentityProvider == null) // If this is online only configured - local not allowed
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }

            _LocalIdentityProvider?.LocalProvider.AddClaim(userName, claim, principal, expiry);
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string userName, string password)
            => Authenticate(userName, password, null);

        /// <inheritdoc/>
        public IPrincipal Authenticate(string userName, string password, string tfaSecret)
        {
            var authenticatingargs = new AuthenticatingEventArgs(userName);
            Authenticating?.Invoke(this, authenticatingargs);

            if (authenticatingargs.Cancel)
            {
                _Tracer.TraceVerbose("Authenticating Event signals cancel.");
                if (authenticatingargs.Success)
                {
                    return authenticatingargs.Principal;
                }
                else
                {
                    throw new AuthenticationException(_LocalizationService.GetString(ErrorMessageStrings.AUTH_CANCELLED));
                }
            }

            IClaimsPrincipal result = null;
            try
            {

                if (ShouldDoRemoteAuthentication(userName))
                {
                    try
                    {
                        result = _OAuthClient.AuthenticateUser(userName, password, tfaSecret: tfaSecret);

                        SynchronizeLocalIdentity(result, password);

                        return result;
                    }
                    catch(RestClientException<OAuthClientTokenResponse> ex)
                    {
                        // HACK: We want to relay the error from upstream
                        throw new RestClientException<Object>(ex.Result, ex, ex.Status, ex.Response);
                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
                    }
                }
                else if (_LocalIdentityProvider == null)
                {
                    throw new UpstreamIntegrationException(String.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
                }

                if (null != tfaSecret)
                {
                    result = _LocalIdentityProvider.LocalProvider.Authenticate(userName, password, tfaSecret) as IClaimsPrincipal;
                }
                else
                {
                    result = _LocalIdentityProvider.LocalProvider.Authenticate(userName, password) as IClaimsPrincipal;
                }

                return result;
            }
            finally
            {
                Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, result, null != result));
            }
        }



        /// <inheritdoc/>
        public void ChangePassword(string userName, string newPassword, IPrincipal principal, bool force)
        {
            _LocalIdentityProvider?.LocalProvider.ChangePassword(userName, newPassword, principal);
            if (ShouldDoRemoteAuthentication(userName))
            {
                ChangeRemotePassword(userName, newPassword);
            }
        }

        /// <inheritdoc/>
        public IIdentity CreateIdentity(string userName, string password, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }

            var result = _LocalIdentityProvider.LocalProvider.CreateIdentity(userName, password, principal);

            _LocalIdentityProvider.LocalProvider.AddClaim(userName, new SanteDBClaim(SanteDBClaimTypes.LocalOnly, "true"), principal);

            result = _LocalIdentityProvider.LocalProvider.GetIdentity(userName);

            return result;
        }

        /// <inheritdoc/>
        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.LocalProvider.DeleteIdentity(userName, principal);
        }

        /// <inheritdoc/>
        public AuthenticationMethod GetAuthenticationMethods(string userName)
        {
            if (null == _LocalIdentityProvider)
            {
                return AuthenticationMethod.Online;
            }

            try
            {
                var claims = _LocalIdentityProvider.LocalProvider.GetClaims(userName);

                if (claims?.Any(c => c.Type == SanteDBClaimTypes.LocalOnly) == true)
                {
                    return AuthenticationMethod.Local;
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {

            }

            return AuthenticationMethod.Local | AuthenticationMethod.Online;
        }

        /// <inheritdoc/>
        public IEnumerable<IClaim> GetClaims(string userName)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.LocalProvider.GetClaims(userName);
        }

        /// <inheritdoc/>
        public IIdentity GetIdentity(string userName)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.LocalProvider.GetIdentity(userName);
        }

        /// <inheritdoc/>
        public IIdentity GetIdentity(Guid sid)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.LocalProvider.GetIdentity(sid);
        }

        /// <inheritdoc/>
        public Guid GetSid(string name)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.LocalProvider.GetSid(name);
        }

        /// <inheritdoc/>
        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal is OAuthClaimsPrincipal oacp)
            {
                if (oacp.CanRefresh)
                {
                    var result = _OAuthClient.Refresh(oacp.GetRefreshToken());

                    SynchronizeLocalIdentity(result, null);

                    return result;
                }
                else
                {
                    throw new NotSupportedException("Cannot refresh this principal");
                }
            }
            else if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            else
            {
                return _LocalIdentityProvider.LocalProvider.ReAuthenticate(principal);
            }
        }

        /// <inheritdoc/>
        public void RemoveClaim(string userName, string claimType, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.LocalProvider.RemoveClaim(userName, claimType, principal);
        }

        /// <inheritdoc/>
        public void SetLockout(string userName, bool lockout, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.LocalProvider.SetLockout(userName, lockout, principal);
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string userName, Guid challengeKey, string response, string tfaSecret)
        {
            var authenticatingargs = new AuthenticatingEventArgs(userName);
            Authenticating?.Invoke(this, authenticatingargs);

            if (authenticatingargs.Cancel)
            {
                _Tracer.TraceVerbose("Authenticating Event signals cancel.");
                if (authenticatingargs.Success)
                {
                    return authenticatingargs.Principal;
                }
                else
                {
                    throw new AuthenticationException(_LocalizationService.GetString(ErrorMessageStrings.AUTH_CANCELLED));
                }
            }

            IClaimsPrincipal result = null;
            try
            {

                if (ShouldDoRemoteAuthentication(userName))
                {
                    try
                    {
                        result = _OAuthClient.ChallengeAuthenticateUser(userName, challengeKey, response, tfaSecret: tfaSecret);
                        return result;
                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
                    }
                }
                else if (_LocalIdentityProvider == null)
                {
                    throw new UpstreamIntegrationException(String.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
                }

                if (null != tfaSecret)
                {
                    result = _LocalSecurityChallengeService.LocalProvider.Authenticate(userName, challengeKey, response, tfaSecret) as IClaimsPrincipal;
                }
                else
                {
                    result = _LocalSecurityChallengeService.LocalProvider.Authenticate(userName, challengeKey, response, tfaSecret) as IClaimsPrincipal;
                }

                return result;
            }
            finally
            {
                Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, result, null != result));
            }
        }
    }
}
