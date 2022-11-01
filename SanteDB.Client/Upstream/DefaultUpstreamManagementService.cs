using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream
{
    /// <summary>
    /// An upstream management service which manages the upstream
    /// </summary>
    [ServiceProvider("Upstream Management", Type = ServiceInstantiationType.PerCall)]
    public class DefaultUpstreamManagementService : IUpstreamManagementService
    {
        private readonly ILocalizationService m_localizationService;
        private readonly ConfiguredUpstreamRealmSettings m_upstreamSettings;
        private readonly IRestClientFactory m_restClientFactory;
        private readonly UpstreamConfigurationSection m_configuration;

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUpstreamManagementService(
            IRestClientFactory restClientFactory,
            IConfigurationManager configurationManager,
            ILocalizationService localizationService
            )
        {
            this.m_restClientFactory = restClientFactory;
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>();
            this.m_localizationService = localizationService;

            if (this.m_configuration?.Realm != null)
            {
                this.m_upstreamSettings = new ConfiguredUpstreamRealmSettings(this.m_configuration);
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "Default Upstream Management Service";

        /// <inheritdoc/>
        public event EventHandler<UpstreamRealmChangedEventArgs> RealmChanged;

        /// <inheritdoc/>
        public bool IsConfigured() => this.m_upstreamSettings != null;

        /// <inheritdoc/>
        public IUpstreamRealmSettings GetSettings() => this.m_upstreamSettings;

        /// <inheritdoc/>
        public void Join(IUpstreamRealmSettings targetRealm, bool replaceExistingRegistration)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void UnJoin()
        {
            throw new NotImplementedException();
        }

    }
}
