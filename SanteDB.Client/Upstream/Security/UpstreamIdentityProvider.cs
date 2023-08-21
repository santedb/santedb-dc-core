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
using SanteDB.Client.Exceptions;
using SanteDB.Client.OAuth;
using SanteDB.Client.Services;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Authentication;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// Represents an implementation of the <see cref="IIdentityProviderService"/> which uses an upstream oauth server
    /// </summary>
    public class UpstreamIdentityProvider : UpstreamServiceBase, IIdentityProviderService, ISecurityChallengeIdentityService, IUpstreamServiceProvider<IIdentityProviderService>, IUpstreamServiceProvider<ISecurityChallengeIdentityService>
    {
        readonly IOAuthClient _OAuthClient;
        readonly ILocalizationService _LocalizationService;
        readonly IUpstreamServiceProvider<IPolicyInformationService> _RemotePolicyInformationService;
        readonly IUpstreamServiceProvider<IRoleProviderService> _RemoteRoleProviderService;


        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamIdentityProvider(
            IOAuthClient oauthClient,
            IRestClientFactory restClientFactory,
            ILocalizationService localizationService,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamServiceProvider<IPolicyInformationService> remotePolicyInformationService,
            IUpstreamServiceProvider<IRoleProviderService> remoteRoleProviderService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider) // on initial configuration in online only mode there are no local users
            : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider)
        {
            _LocalizationService = localizationService;
            _RemotePolicyInformationService = remotePolicyInformationService;
            _RemoteRoleProviderService = remoteRoleProviderService;
            //_SecurityRepositoryService = securityRepositoryService;
            _OAuthClient = oauthClient ?? throw new ArgumentNullException(nameof(oauthClient));
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Identity Provider";

        /// <inheritdoc/>
        IIdentityProviderService IUpstreamServiceProvider<IIdentityProviderService>.UpstreamProvider => this;

        /// <inheritdoc/>
        ISecurityChallengeIdentityService IUpstreamServiceProvider<ISecurityChallengeIdentityService>.UpstreamProvider => this;

        /// <inheritdoc/>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        /// <inheritdoc/>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <inheritdoc/>
        public void AddClaim(string userName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            throw new NotSupportedException();
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

            IPrincipal result = null;

            try
            {
                result = _OAuthClient.AuthenticateUser(userName, password, tfaSecret: tfaSecret);
                return result;
            }
            catch (RestClientException<OAuthClientTokenResponse> ex)
            {
                // HACK: We want to relay the error from upstream
                throw new RestClientException<Object>(ex.Result, ex, ex.Status, ex.Response);
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
            }
            finally
            {
                Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, result, null != result));

            }

        }

        /// <summary>
        /// Get upstream user based on the user name
        /// </summary>
        private SecurityUser GetUpstreamSecurityUser(Expression<Func<SecurityUser, bool>> query, IPrincipal principal)
        {
            using (var amiclient = base.CreateAmiServiceClient())
            {
                try
                {
                    return amiclient.GetUsers(query).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault()?.Entity;
                }
                catch (Exception e)
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = query }), e);
                }
            }
        }

        /// <summary>
        /// Convert to identity
        /// </summary>
        private IIdentity ConvertToIdentity(SecurityUser userInfo)
        {
            var claims = userInfo.Roles.Select(o => new SanteDBClaim(SanteDBClaimTypes.DefaultRoleClaimType, o.Name)).ToList();
            claims.Add(new SanteDBClaim(SanteDBClaimTypes.SecurityId, userInfo.Key.ToString()));
            return new SanteDBClaimsIdentity(userInfo.UserName, false, "NONE", claims);
        }

        /// <inheritdoc/>
        public void ChangePassword(string userName, string newPassword, IPrincipal principal, bool force)
        {
            var remoteUser = this.GetUpstreamSecurityUser(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant(), principal);
            if (remoteUser == null)
            {
                throw new KeyNotFoundException(userName);
            }
            remoteUser.Password = newPassword;
            using (var amiclient = CreateAmiServiceClient())
            {
                try
                {
                    var result = amiclient.UpdateUser(remoteUser.Key.Value, new Core.Model.AMI.Auth.SecurityUserInfo(remoteUser) { PasswordOnly = true });
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = remoteUser.Key.Value }), ex);
                }
            }
        }

        /// <inheritdoc/>
        public IIdentity CreateIdentity(string userName, string password, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            throw new NotSupportedException();

        }

        /// <inheritdoc/>
        public AuthenticationMethod GetAuthenticationMethods(string userName) => AuthenticationMethod.Online;

        /// <inheritdoc/>
        public IEnumerable<IClaim> GetClaims(string userName)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IIdentity GetIdentity(string userName)
        {
            var remoteUser = this.GetUpstreamSecurityUser(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant(), AuthenticationContext.Current.Principal);
            if (remoteUser != null)
            {
                return this.ConvertToIdentity(remoteUser);
            }
            return null;
        }

        /// <inheritdoc/>
        public IIdentity GetIdentity(Guid sid)
        {
            var remoteUser = this.GetUpstreamSecurityUser(o => o.Key == sid, AuthenticationContext.Current.Principal);
            if (remoteUser != null)
            {
                return this.ConvertToIdentity(remoteUser);
            }
            return null;
        }

        /// <inheritdoc/>
        public Guid GetSid(string name) => this.GetUpstreamSecurityUser(o => o.UserName == name.ToLowerInvariant(), AuthenticationContext.Current.Principal)?.Key ?? Guid.Empty;

        /// <inheritdoc/>
        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal is OAuthClaimsPrincipal oacp)
            {
                if (oacp.CanRefresh)
                {
                    var result = _OAuthClient.Refresh(oacp.GetRefreshToken());
                    return result;
                }
                else
                {
                    throw new NotSupportedException("Cannot refresh this principal");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(principal), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(OAuthClaimsPrincipal), principal.GetType()));
            }
        }

        /// <inheritdoc/>
        public void RemoveClaim(string userName, string claimType, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void SetLockout(string userName, bool lockout, IPrincipal principal)
        {
            throw new NotSupportedException();
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
                result = _OAuthClient.ChallengeAuthenticateUser(userName, challengeKey, response, tfaSecret: tfaSecret);
                return result;
            }
            catch (RestClientException<OAuthClientTokenResponse> ex)
            {
                throw new RestClientException<Object>(ex.Result, ex, ex.Status, ex.Response);
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
            }
            finally
            {
                Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, result, null != result));
            }
        }
    }
}
