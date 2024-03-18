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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// Remote security challenge provider repository
    /// </summary>
    [PreferredService(typeof(ISecurityChallengeService))]
    public class UpstreamSecurityChallengeProvider : UpstreamServiceBase, ISecurityChallengeService, IUpstreamServiceProvider<ISecurityChallengeService>
    {
        private readonly ILocalServiceProvider<ISecurityChallengeService> m_localSecurityChallengeService;
        private readonly IIdentityProviderService m_identityProvider;
        private readonly ISecurityRepositoryService m_securityRepository;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(UpstreamSecurityChallengeProvider));

        /// <inheritdoc/>
        public ISecurityChallengeService UpstreamProvider => this;

        /// <inheritdoc/>
        public string ServiceName => "Upstream Security Challenge Provider";

        /// <summary>
        /// Gets the upstream integration service
        /// </summary>
        public UpstreamSecurityChallengeProvider(
            IRestClientFactory restClientFactory,
            IIdentityProviderService identityProvider,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            ISecurityRepositoryService securityRepositoryService = null,
            IUpstreamIntegrationService upstreamIntegrationService = null,
            ILocalServiceProvider<ISecurityChallengeService> localSecurityChallengeService = null
            ) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_securityRepository = securityRepositoryService;
            this.m_localSecurityChallengeService = localSecurityChallengeService;
            this.m_identityProvider = identityProvider;
        }

        /// <inheritdoc/>
        public IEnumerable<SecurityChallenge> Get(string userName, IPrincipal principal)
        {
            // Try to gather whether the user is upstream or not
            if (!this.m_identityProvider.GetAuthenticationMethods(userName).HasFlag(AuthenticationMethod.Online))
            {
                return this.m_localSecurityChallengeService.LocalProvider.Get(userName, principal);
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not configured - skipping");
                return new SecurityChallenge[0];
            }
            else
            {
                var sid = this.m_identityProvider.GetSid(userName);
                return this.Get(sid, principal);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<SecurityChallenge> Get(Guid userKey, IPrincipal principal)
        {
            if (!this.m_identityProvider.GetAuthenticationMethods(this.m_securityRepository.ResolveName(userKey))
                .HasFlag(AuthenticationMethod.Online))
            {
                return this.m_localSecurityChallengeService.LocalProvider.Get(userKey, principal);
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not configured - skipping");
                return new SecurityChallenge[0];
            }
            else
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                {
                    return client.Get<AmiCollection>($"SecurityUser/{userKey}/challenge").CollectionItem.OfType<SecurityChallenge>();
                }
            }

        }

        /// <inheritdoc/>
        public void Remove(string userName, Guid challengeKey, IPrincipal principal)
        {
            // Is this user a local user?
            // Try to gather whether the user is upstream or not
            if (!this.m_identityProvider.GetAuthenticationMethods(userName).HasFlag(AuthenticationMethod.Online))
            {
                this.m_localSecurityChallengeService.LocalProvider.Remove(userName, challengeKey, principal);
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not configured - skipping");
            }
            else
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                {
                    var sid = this.m_identityProvider.GetSid(userName);
                    client.Delete<SecurityChallenge>($"SecurityUser/{sid}/challenge/{challengeKey}");
                }
            }
        }

        /// <inheritdoc/>
        public void Set(string userName, Guid challengeKey, string response, IPrincipal principal)
        {
            // Is this user a local user?
            if (!this.m_identityProvider.GetAuthenticationMethods(userName).HasFlag(AuthenticationMethod.Online))
            {
                this.m_localSecurityChallengeService.LocalProvider.Set(userName, challengeKey, response, principal);
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not configured - skipping");
            }
            else
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                {
                    var sid = this.m_identityProvider.GetSid(userName);
                    var challengeSet = new SecurityUserChallengeInfo()
                    {
                        ChallengeKey = challengeKey,
                        ChallengeResponse = response
                    };

                    client.Post<SecurityUserChallengeInfo, Object>($"SecurityUser/{sid}/challenge", challengeSet);
                }
            }
        }
    }
}
