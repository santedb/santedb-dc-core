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
using SanteDB.Client.OAuth;
using SanteDB.Client.Services;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.OAuth;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using System.Security.Authentication;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// Represents an implementation of a <see cref="IApplicationIdentityProviderService"/> which uses OAUTH
    /// </summary>
    public class UpstreamApplicationIdentityProvider : UpstreamServiceBase, IApplicationIdentityProviderService, IUpstreamServiceProvider<IApplicationIdentityProviderService>
    {
        readonly IOAuthClient _OAuthClient;
        readonly ILocalizationService _LocalizationService;

        /// <summary>
        /// Upstream application identity used for GetIdentity calls
        /// </summary>
        private class UpstreamApplicationIdentity : SanteDBClaimsIdentity, IApplicationIdentity
        {
            /// <summary>
            /// Create a new 
            /// </summary>
            /// <param name="application"></param>
            public UpstreamApplicationIdentity(SecurityApplication application)
                : base(application.Name, false, "NONE")
            {
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.Actor, ActorTypeKeys.Application.ToString()));
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.SecurityId, application.Key.ToString()));
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.NameIdentifier, application.Key.ToString()));
            }
        }

        /// <inheritdoc/>
        public IApplicationIdentityProviderService UpstreamProvider => this;

        /// <summary>
        /// DIConstructor
        /// </summary>
        public UpstreamApplicationIdentityProvider(
            IOAuthClient oauthClient,
            IRestClientFactory restClientFactory,
            ILocalizationService localizationService,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService) // on initial configuration in online only mode there are no local users
            : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            _LocalizationService = localizationService;
            _OAuthClient = oauthClient ?? throw new ArgumentNullException(nameof(oauthClient));

        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Application Identity Provider";

        /// <inheritdoc />
        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        /// <inheritdoc />
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        /// <summary>
        /// Get upstream application based on the name
        /// </summary>
        private SecurityApplicationInfo GetUpstreamSecurityApplication(Expression<Func<SecurityApplication, bool>> query, IPrincipal principal)
        {
            using (var amiclient = base.CreateAmiServiceClient())
            {
                try
                {
                    return amiclient.GetApplications(query).CollectionItem.OfType<SecurityApplicationInfo>().FirstOrDefault();
                }
                catch (Exception e)
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = query }), e);
                }
            }
        }
        /// <inheritdoc/>
        public void AddClaim(string applicationName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            throw new NotSupportedException(ErrorMessageStrings.UPSTREAM_CLAIMS_READONLY);
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string clientId, string clientSecret)
            => AuthenticateInternal(clientId, clientSecret);

        /// <inheritdoc/>
        public IPrincipal Authenticate(string clientId, IPrincipal authenticationContext)
            => AuthenticateInternal(clientId, null, authenticationContext);

        private IPrincipal AuthenticateInternal(string clientId, string clientSecret = null, IPrincipal authenticationContext = null, IIdentity onBehalfOf = null)
        {
            var authenticatingargs = new AuthenticatingEventArgs(clientId);
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
                if (null != clientSecret)
                {
                    result = _OAuthClient.AuthenticateApp(clientId, clientSecret);
                }
                else if (null != authenticationContext)
                {
                    using (AuthenticationContext.EnterContext(authenticationContext)) // Enter authentication context so the rest client knows to append the proper headers
                    {
                        result = _OAuthClient.AuthenticateApp(clientId, null, clientClaimAssertions: new IClaim[] { new SanteDBClaim(SanteDBClaimTypes.OnBehalfOf, authenticationContext.Identity.Name) });
                    }
                }
                return result;
            }
            catch (RestClientException<OAuthTokenResponse> e)
            {
                throw new RestClientException<Object>(e.Result, e, e.Status, e.Response);
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
            }
            finally
            {
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(clientId, result, null != result));
            }
        }

        /// <inheritdoc/>
        public void ChangeSecret(string applicationName, string secret, IPrincipal principal)
        {
            using (var amiclient = CreateAmiServiceClient())
            {
                var remoteapp = amiclient.GetApplications(app => app.Name.ToLowerInvariant() == applicationName.ToLowerInvariant())?.CollectionItem?.OfType<SecurityApplicationInfo>()?.FirstOrDefault();

                if (null == remoteapp?.Key)
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(SecurityApplication) }));
                }

                remoteapp.Entity.ApplicationSecret = secret;

                try
                {
                    amiclient.UpdateApplication(remoteapp.Entity.Key.Value, remoteapp);
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = remoteapp.Entity.Key.Value }), ex);
                }
            }
        }

        /// <inheritdoc/>
        public IApplicationIdentity CreateIdentity(string applicationName, string password, IPrincipal principal, Guid? withSid = null)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IEnumerable<IClaim> GetClaims(string applicationName)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IApplicationIdentity GetIdentity(string applicationName)
        {
            var remoteApplication = this.GetUpstreamSecurityApplication(o => o.Name.ToLowerInvariant() == applicationName.ToLowerInvariant(), AuthenticationContext.Current.Principal);
            if (remoteApplication != null)
            {
                return new UpstreamApplicationIdentity(remoteApplication.Entity);
            }
            return null;
        }

        /// <inheritdoc/>
        public IApplicationIdentity GetIdentity(Guid sid)
        {
            var remoteApplication = this.GetUpstreamSecurityApplication(o => o.Key == sid, AuthenticationContext.Current.Principal);
            if (remoteApplication != null)
            {
                return new UpstreamApplicationIdentity(remoteApplication.Entity);
            }
            return null;
        }

        /// <inheritdoc/>
        public Guid GetSid(string name)
        {
            return this.GetUpstreamSecurityApplication(o => o.Name.ToLowerInvariant() == name.ToLowerInvariant(), AuthenticationContext.Current.Principal)?.Key ?? Guid.Empty;
        }

        /// <inheritdoc/>
        public void RemoveClaim(string applicationName, string claimType, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void SetLockout(string applicationName, bool lockoutState, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal is OAuthClaimsPrincipal oacp && oacp.CanRefresh)
            {
                var result = _OAuthClient.Refresh(oacp.GetRefreshToken());
                return result;
            }
            else
            {
                throw new NotSupportedException("Cannot refresh this principal");
            }

        }

    }
}
