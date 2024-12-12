using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Query;
using SanteDB.Core.PubSub;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.Templates;
using SanteDB.Core.Templates.Definition;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// Upstream data template management service
    /// </summary>
    public class UpstreamDataTemplateManagementService : UpstreamServiceBase, IDataTemplateManagementService
    {
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamDataTemplateManagementService(IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, IUpstreamIntegrationService upstreamIntegrationService = null, ILocalizationService localizationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localizationService = localizationService;
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Data Template Manager";

        /// <inheritdoc/>
        public DataTemplateDefinition AddOrUpdate(DataTemplateDefinition definition)
        {
            if(definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            try
            {
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Post<DataTemplateDefinition, DataTemplateDefinition>(typeof(DataTemplateDefinition).GetSerializationName(), definition);
                }
            }
            catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = definition }), e);
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<DataTemplateDefinition> Find(Expression<Func<DataTemplateDefinition, bool>> query)
        {
            return new UpstreamQueryResultSet<DataTemplateDefinition, AmiCollection>(this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal), query);
        }

        /// <inheritdoc/>
        public DataTemplateDefinition Get(Guid key)
        {
            if(key == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(key));
            }

            try
            {
                using(var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Get<DataTemplateDefinition>($"{typeof(DataTemplateDefinition).GetSerializationName()}/{key}");
                }
            }
            catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = typeof(DataTemplateDefinition).GetSerializationName() }), e);
            }
        }

        /// <inheritdoc/>
        public DataTemplateDefinition Remove(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(key));
            }

            try
            {
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Delete<DataTemplateDefinition>($"{typeof(DataTemplateDefinition).GetSerializationName()}/{key}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = typeof(DataTemplateDefinition).GetSerializationName() }), e);
            }
        }
    }
}
