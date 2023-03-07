using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Matching;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Matcher.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Upstream.Matching
{
    /// <summary>
    /// Upstream record matching configuration service
    /// </summary>
    public class UpstreamMatchConfigurationService : UpstreamServiceBase, IRecordMatchingConfigurationService
    {
        // Localization service
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamMatchConfigurationService(ILocalizationService localizationService, IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localizationService = localizationService;
        }

        /// <inheritdoc/>
        public IEnumerable<IRecordMatchingConfiguration> Configurations
        {
            get
            {
                try
                {
                    using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                    {
                        return client.Get<AmiCollection>("MatchConfiguration").CollectionItem.OfType<IRecordMatchingConfiguration>();
                    }
                }
                catch(Exception e)
                {
                    throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = "MatchConfiguration" }), e);
                }
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Match Configuration Service";

        /// <inheritdoc/>
        public IRecordMatchingConfiguration DeleteConfiguration(string configurationId)
        {
            try
            {
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Delete<MatchConfiguration>($"MatchConfiguration/{configurationId}");
                }
            }
            catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = "MatchConfiguration" }), e);
            }
        }

        /// <inheritdoc/>
        public IRecordMatchingConfiguration GetConfiguration(string configurationId)
        {
            try
            {
                using(var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Get<MatchConfiguration>($"MatchConfiguration/{configurationId}");
                }
            }
            catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = "MatchConfiguration" }), e);
            }
         }

        /// <inheritdoc/>
        public IRecordMatchingConfiguration SaveConfiguration(IRecordMatchingConfiguration configuration)
        {
            try
            {
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    if (configuration is MatchConfiguration sc)
                    {
                        return client.Put<MatchConfiguration, MatchConfiguration>($"MatchConfiguration/{sc.Id}", sc);
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(configuration), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(MatchConfiguration), configuration.GetType()));
                    }
                }
            }
             catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = "MatchConfiguration" }), e);
            }
        }
    }
}
