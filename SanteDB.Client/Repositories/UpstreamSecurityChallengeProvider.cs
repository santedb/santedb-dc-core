using SanteDB.Client.Http;
using SanteDB.Core.Http;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Repositories
{
    /// <summary>
    /// Remote security challenge provider repository
    /// </summary>
    [PreferredService(typeof(ISecurityChallengeService))]
    public class UpstreamSecurityChallengeProvider : ISecurityChallengeService
    {
        private readonly ISecurityChallengeService m_localSecurityChallengeService;
        private readonly IUpstreamIntegrationService m_upstreamIntegrationService;
        private readonly IIdentityProviderService m_identityProvider;
        private readonly ISecurityRepositoryService m_securityRepository;
        private readonly IRestClientFactory m_restClientFactory;

        /// <inheritdoc/>
        public string ServiceName => "Upstream Security Challenge Provider";

        /// <summary>
        /// Gets the upstream integration service
        /// </summary>
        public UpstreamSecurityChallengeProvider( 
            IUpstreamIntegrationService upstreamIntegrationService,
            IRestClientFactory restClientFactory,
            IIdentityProviderService identityProvider,
            ISecurityRepositoryService securityRepository = null,
            ISecurityChallengeService localSecurityChallengeService = null)
        {
            this.m_localSecurityChallengeService = localSecurityChallengeService;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
            this.m_identityProvider = identityProvider;
            this.m_securityRepository = securityRepository;
            this.m_restClientFactory = restClientFactory;
        }

        /// <inheritdoc/>
        public IEnumerable<SecurityChallenge> Get(string userName, IPrincipal principal)
        {
            // Try to gather whether the user is upstream or not
            if(!this.m_identityProvider.GetAuthenticationMethods(userName).HasFlag(AuthenticationMethod.Online))
            {
                return this.m_localSecurityChallengeService.Get(userName, principal);
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
            if (!this.m_identityProvider.GetAuthenticationMethods(this.m_identityProvider.GetIdentity(userKey).Name)
                .HasFlag(AuthenticationMethod.Online))
            {
                return this.m_localSecurityChallengeService.Get(userKey, principal);
            }
            else
            {
                using (var client = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    client.Credentials = new UpstreamPrincipalCredentials(principal);
                    using (AuthenticationContext.EnterContext(principal))
                    {
                        return client.Get<AmiCollection>($"SecurityUser/{userKey}/challenge").CollectionItem.OfType<SecurityChallenge>();
                    }
                }
            }

        }

        /// <inheritdoc/>
        public void Remove(string userName, Guid challengeKey, IPrincipal principal)
        {
            // Is this user a local user?
            if (!this.m_identityProvider.GetAuthenticationMethods(userName).HasFlag(AuthenticationMethod.Online))
            {
                this.m_localSecurityChallengeService.Remove(userName, challengeKey, principal);
            }
            else
            {
                using (var client = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    var sid = this.m_identityProvider.GetSid(userName);
                    client.Credentials = new UpstreamPrincipalCredentials(principal);
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
                this.m_localSecurityChallengeService.Set(userName, challengeKey, response, principal);
            }
            else
            {
                using (var client = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    var sid = this.m_identityProvider.GetSid(userName);
                    client.Credentials = new UpstreamPrincipalCredentials(principal);
                    
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
