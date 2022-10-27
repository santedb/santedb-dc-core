using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
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

        /// <inheritdoc/>
        public string ServiceName => "Upstream Security Challenge Provider";

        /// <summary>
        /// Gets the upstream integration service
        /// </summary>
        /// <param name="upstreamIntegrationService"></param>
        public UpstreamSecurityChallengeProvider(ISecurityChallengeService localSecurityChallengeService, IUpstreamIntegrationService upstreamIntegrationService)
        {
            this.m_localSecurityChallengeService = localSecurityChallengeService;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
        }

        /// <inheritdoc/>
        public IEnumerable<SecurityChallenge> Get(string userName, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IEnumerable<SecurityChallenge> Get(Guid userKey, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Remove(string userName, Guid challengeKey, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Set(string userName, Guid challengeKey, string response, IPrincipal principal)
        {
            throw new NotImplementedException();
        }
    }
}
