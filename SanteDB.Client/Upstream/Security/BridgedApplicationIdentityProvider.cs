/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-6-21
 */
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
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
    /// Application identity provider service that bridges between local and upstream
    /// </summary>
    [PreferredService(typeof(IApplicationIdentityProviderService))]
    public class BridgedApplicationIdentityProvider : UpstreamServiceBase, IApplicationIdentityProviderService
    {
        private readonly object m_lockObject = new object();
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(BridgedApplicationIdentityProvider));
        private readonly IApplicationIdentityProviderService m_localApplicationIdentityProvider;
        private readonly IApplicationIdentityProviderService m_upstreamApplicationIdentityProvider;
        private readonly ILocalizationService m_localizationService;
        private readonly IPolicyInformationService m_upstreamPip;
        private readonly IPolicyInformationService m_localPip;
        private readonly UpstreamCredentialConfiguration m_configuration;

        /// <summary>
        /// DI ctor
        /// </summary>
        public BridgedApplicationIdentityProvider(IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamServiceProvider<IApplicationIdentityProviderService> upstreamApplicationIdentityProivder,
            ILocalServiceProvider<IApplicationIdentityProviderService> localApplicationIdentityProvider,
            IUpstreamServiceProvider<IPolicyInformationService> upstreamPip,
            ILocalServiceProvider<IPolicyInformationService> localPip,
            IConfigurationManager configurationManager,
            ILocalizationService localizationService) :
            base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider)
        {
            this.m_upstreamPip = upstreamPip.UpstreamProvider;
            this.m_localPip = localPip.LocalProvider;
            this.m_localApplicationIdentityProvider = localApplicationIdentityProvider.LocalProvider;
            this.m_upstreamApplicationIdentityProvider = upstreamApplicationIdentityProivder.UpstreamProvider;
            this.m_localizationService = localizationService;
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>().Credentials.Find(o => o.CredentialType == UpstreamCredentialType.Application);

            // Attempt to get the local record for this client and update if required
            this.m_tracer.TraceInfo("Initializing local application credential...");
            if (this.m_localApplicationIdentityProvider.GetIdentity(this.m_configuration.CredentialName) == null)
            {
                using (AuthenticationContext.EnterSystemContext())
                {
                    var sid = this.m_upstreamApplicationIdentityProvider.GetSid(this.m_configuration.CredentialName);
                    this.m_localApplicationIdentityProvider.CreateIdentity(this.m_configuration.CredentialName, this.m_configuration.CredentialSecret, AuthenticationContext.SystemPrincipal, sid);
                }
            }
        }

        /// <inheritdoc/>
        public string ServiceName => nameof(BridgedApplicationIdentityProvider);

        /// <inheritdoc/>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        /// <inheritdoc/>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        /// <summary>
        /// Synchronize the identity between the remote identity and the local system
        /// </summary>
        private void SynchronizeIdentity(IClaimsPrincipal remoteIdentity, string secret)
        {
            using (remoteIdentity.Identity.Name == this.m_configuration.CredentialName ?
                AuthenticationContext.EnterContext(remoteIdentity) :
                AuthenticationContext.EnterSystemContext())
            {
                if (remoteIdentity == null)
                {
                    throw new ArgumentNullException(nameof(remoteIdentity));
                }

                // This is done to prevent circular dependencies
                var upstreamSecurityRepository = ApplicationServiceContext.Current.GetService<IUpstreamServiceProvider<ISecurityRepositoryService>>()?.UpstreamProvider;
                var localSecurityRepository = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityApplication>>();

                // Get the user identity 
                var applicationIdentity = remoteIdentity.Identities.FirstOrDefault(o => o.FindFirst(SanteDBClaimTypes.Actor).Value == ActorTypeKeys.Application.ToString());
                if (applicationIdentity == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(SynchronizeIdentity)));
                }

                var localApplication = localSecurityRepository.Find(o => o.Name.ToLowerInvariant() == applicationIdentity.Name.ToLowerInvariant() && o.ObsoletionTime == null).FirstOrDefault();
                if (localApplication == null)
                {
                    var upstreamApplicationInfo = upstreamSecurityRepository.GetApplication(applicationIdentity.Name);
                    localApplication = localSecurityRepository.Insert(upstreamApplicationInfo);
                }

                if (!String.IsNullOrEmpty(secret))
                {
                    this.m_localApplicationIdentityProvider.ChangeSecret(applicationIdentity.Name, secret, AuthenticationContext.SystemPrincipal);
                }

                // Synchronize the policies 
                lock (this.m_lockObject)
                {
                    var remotePolicies = this.m_upstreamPip.GetPolicies(localApplication);
                    // Remove all 
                    var localPolicies = this.m_localPip.GetPolicies();
                    this.m_localPip.RemovePolicies(localApplication, AuthenticationContext.SystemPrincipal, localPolicies.Select(o => o.Oid).ToArray());
                    foreach (var itm in remotePolicies.GroupBy(o => o.Rule))
                    {
                        this.m_localPip.AddPolicies(localApplication, itm.Key, AuthenticationContext.SystemPrincipal, itm.Select(o => o.Policy.Oid).ToArray());
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if <paramref name="applicationName"/> is a local only identity
        /// </summary>
        private bool IsLocalApplication(string applicationName)
        {
            return this.m_localApplicationIdentityProvider.GetClaims(applicationName)?.Any(c => c.Type == SanteDBClaimTypes.LocalOnly) == true;
        }

        /// <summary>
        /// True if remote authentication should be performed
        /// </summary>
        private bool ShouldDoRemoteAuthentication(string applicationName)
        {
            return !this.IsLocalApplication(applicationName) &&
                base.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AuthenticationService);
        }

        /// <inheritdoc/>
        public void AddClaim(string applicationName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            this.m_localApplicationIdentityProvider.AddClaim(applicationName, claim, principal, expiry);
            if (!this.IsLocalApplication(applicationName))
            {
                this.m_tracer.TraceWarning("Claim on identity {0} only applies to local device", applicationName);
            }
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string applicationName, string applicationSecret) => this.AuthenticateInternal(applicationName, applicationSecret, null);

        /// <summary>
        /// Perform application logic
        /// </summary>
        private IPrincipal AuthenticateInternal(string applicationName, string applicationSecret, IPrincipal authenticationUnder)
        {
            if (String.IsNullOrEmpty(applicationName))
            {
                throw new ArgumentNullException(nameof(applicationName));
            }
            else if (authenticationUnder == null && String.IsNullOrEmpty(applicationSecret))
            {
                throw new ArgumentException(nameof(applicationSecret));
            }

            var authenticatingArgs = new AuthenticatingEventArgs(applicationName);
            this.Authenticating?.Invoke(this, authenticatingArgs);
            if (authenticatingArgs.Cancel)
            {
                this.m_tracer.TraceVerbose("Authenticating event signals cancel");
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
                if (this.ShouldDoRemoteAuthentication(applicationName) && (authenticationUnder == null || authenticationUnder is ITokenPrincipal)) // Only token principals can auth upstream so don't bother 
                {
                    try
                    {
                        result = authenticationUnder != null ? this.m_upstreamApplicationIdentityProvider.Authenticate(applicationName, authenticationUnder) :
                            this.m_upstreamApplicationIdentityProvider.Authenticate(applicationName, applicationSecret);
                        this.SynchronizeIdentity(result as IClaimsPrincipal, applicationSecret);
                    }
                    catch (RestClientException<Object>)
                    {
                        throw;
                    }
                    catch (UpstreamIntegrationException e) when (e.InnerException is TimeoutException)
                    {
                        result = authenticationUnder != null ? this.m_localApplicationIdentityProvider.Authenticate(applicationName, authenticationUnder) :
                                this.m_localApplicationIdentityProvider.Authenticate(applicationName, applicationSecret);
                    }
                    catch (TimeoutException)
                    {
                        result = authenticationUnder != null ? this.m_localApplicationIdentityProvider.Authenticate(applicationName, authenticationUnder) :
                                this.m_localApplicationIdentityProvider.Authenticate(applicationName, applicationSecret);
                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
                    }
                }
                else
                {
                    result = authenticationUnder != null ? this.m_localApplicationIdentityProvider.Authenticate(applicationName, authenticationUnder) :
                            this.m_localApplicationIdentityProvider.Authenticate(applicationName, applicationSecret);
                }
            }
            finally
            {
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(applicationName, result, result != null));
            }
            return result;

        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string applicationName, IPrincipal authenticationContext) => this.AuthenticateInternal(applicationName, null, authenticationContext);

        /// <inheritdoc/>
        public void ChangeSecret(string applicationName, string secret, IPrincipal principal)
        {
            // Changing secret on a bridged application only applies if the application has been logged in
            if (this.m_localApplicationIdentityProvider.GetIdentity(applicationName) == null)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(ChangeSecret)));
            }

            // Change upstream first if not local 
            if (!this.IsLocalApplication(applicationName))
            {
                this.m_upstreamApplicationIdentityProvider.ChangeSecret(applicationName, secret, principal);
            }
            // Change locally 
            this.m_localApplicationIdentityProvider.ChangeSecret(applicationName, secret, principal);
        }

        /// <inheritdoc/>
        public IApplicationIdentity CreateIdentity(string applicationName, string password, IPrincipal principal, Guid? withSid = null)
        {
            var localIdentity = this.m_localApplicationIdentityProvider.CreateIdentity(applicationName, password, principal, withSid);
            this.m_localApplicationIdentityProvider.AddClaim(applicationName, new SanteDBClaim(SanteDBClaimTypes.LocalOnly, "true"), principal);
            return this.m_localApplicationIdentityProvider.GetIdentity(applicationName);
        }

        /// <inheritdoc/>
        public IEnumerable<IClaim> GetClaims(string applicationName) => this.m_localApplicationIdentityProvider.GetClaims(applicationName);

        /// <inheritdoc/>
        public IApplicationIdentity GetIdentity(string applicationName)
        {
            if (this.IsLocalApplication(applicationName) || !this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                return this.m_localApplicationIdentityProvider.GetIdentity(applicationName);
            }
            else
            {
                return this.m_upstreamApplicationIdentityProvider.GetIdentity(applicationName);
            }
        }

        /// <inheritdoc/>
        public IApplicationIdentity GetIdentity(Guid sid)
        {
            return this.m_localApplicationIdentityProvider.GetIdentity(sid) ??
                this.m_upstreamApplicationIdentityProvider.GetIdentity(sid);
        }

        /// <inheritdoc/>
        public Guid GetSid(string name)
        {
            if (this.IsLocalApplication(name) || !this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                return this.m_localApplicationIdentityProvider.GetSid(name);
            }
            else
            {
                return this.m_upstreamApplicationIdentityProvider.GetSid(name);
            }
        }

        /// <inheritdoc/>
        public void RemoveClaim(string applicationName, string claimType, IPrincipal principal)
        {
            this.m_localApplicationIdentityProvider.RemoveClaim(applicationName, claimType, principal);
            if (!this.IsLocalApplication(applicationName))
            {
                this.m_tracer.TraceWarning("Claim on identity {0} only applies to local device", applicationName);
            }
        }

        /// <inheritdoc/>
        public void SetLockout(string applicationName, bool lockoutState, IPrincipal principal)
        {
            this.m_localApplicationIdentityProvider.SetLockout(applicationName, lockoutState, principal);
            if (!this.IsLocalApplication(applicationName))
            {
                this.m_tracer.TraceWarning("Identity {0} only lockout on local device", applicationName);
            }
        }

    }
}
