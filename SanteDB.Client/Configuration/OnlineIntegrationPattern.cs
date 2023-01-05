using SanteDB.Client.Repositories;
using SanteDB.Client.Upstream.Management;
using SanteDB.Client.Upstream.Matching;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Client.Upstream.Security;
using SanteDB.Core.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// Online
    /// </summary>
    public class OnlineIntegrationPattern : IUpstreamIntegrationPattern
    {
        /// <inheritdoc/>
        public string Name => "online";

        /// <inheritdoc/>
        public IEnumerable<Type> GetServices() =>
                    new Type[] {
                        typeof(UpstreamJobManager),
                        typeof(UpstreamForeignDataManagement),
                        typeof(UpstreamRepositoryFactory),
                        typeof(UpstreamIdentityProvider),
                        typeof(UpstreamApplicationIdentityProvider),
                        typeof(UpstreamPolicyInformationService),
                        typeof(UpstreamRoleProviderService),
                        typeof(UpstreamMatchConfigurationService),
                        typeof(UpstreamSecurityRepository),
                        typeof(UpstreamSecurityChallengeProvider),
                        typeof(RepositoryEntitySource)
                    };
    }
}
