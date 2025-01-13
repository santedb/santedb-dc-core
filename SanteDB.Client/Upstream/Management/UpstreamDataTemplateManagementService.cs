using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
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

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Delete(Guid key) => this.Remove(key);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Delete(Guid key) => this.Remove(key);

        /// <inheritdoc/>
        IQueryResultSet<DataTemplateDefinition> IRepositoryService<DataTemplateDefinition>.Find(Expression<Func<DataTemplateDefinition, bool>> query) => this.Find(query);

        /// <inheritdoc/>
        IQueryResultSet IRepositoryService.Find(Expression query)
        {
            if (query is Expression<Func<DataTemplateDefinition, bool>> qr)
            {
                return this.Find(qr);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(Expression<Func<DataTemplateDefinition, bool>>), query.GetType()));
            }
        }

        /// <inheritdoc/>
        IEnumerable<IdentifiedData> IRepositoryService.Find(Expression query, int offset, int? count, out int totalResults)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Get(Guid key) => this.Get(key);

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Get(Guid key, Guid versionKey) => this.Get(key);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Get(Guid key) => this.Get(key);

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Insert(DataTemplateDefinition data) => this.AddOrUpdate(data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Insert(object data)
        {
            if (data is DataTemplateDefinition dd)
            {
                return this.AddOrUpdate(dd);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DataTemplateDefinition), data.GetType()));
            }
        }

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Save(DataTemplateDefinition data) => this.AddOrUpdate(data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Save(object data)
        {
            if (data is DataTemplateDefinition dd)
            {
                return this.AddOrUpdate(dd);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DataTemplateDefinition), data.GetType()));
            }
        }
    }
}
