using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.HDSI.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// An upstream integration service
    /// </summary>
    public class DefaultUpstreamIntegrationService : IUpstreamIntegrationService
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(DefaultUpstreamIntegrationService));
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IUpstreamManagementService m_upstreamManager;
        private readonly UpstreamConfigurationSection m_configuration;
        private readonly ILocalizationService m_localizationService;
        private readonly INetworkInformationService m_networkInformationService;
        private readonly IServiceManager m_serviceManager;
        private ConcurrentDictionary<String, Guid> s_templateKeys = new ConcurrentDictionary<string, Guid>();
       
        /// <inheritdoc/>
        public string ServiceName => "Upstream Data Provider";

        /// <inheritdoc/>
        public event EventHandler<RestResponseEventArgs> Responding;
        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;
        /// <inheritdoc/>
        public event EventHandler<UpstreamIntegrationResultEventArgs> Responded;

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUpstreamIntegrationService(IRestClientFactory restClientFactory,
            INetworkInformationService networkInformationService,
            IConfigurationManager configurationManager,
            IServiceManager serviceManager,
            IUpstreamManagementService upstreamManagementService,
            ILocalizationService localizationService)
        {
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>();
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamManager = upstreamManagementService;
            this.m_localizationService = localizationService;
            this.m_networkInformationService = networkInformationService;
            this.m_serviceManager = serviceManager;


        }

        /// <inheritdoc/>
        public IQueryResultSet<IdentifiedData> Find(Type modelType, NameValueCollection filter, UpstreamIntegrationOptions options = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<IdentifiedData> Find<TModel>(NameValueCollection filter, int offset, int? count, UpstreamIntegrationOptions options = null) where TModel : IdentifiedData
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<IdentifiedData> Find<TModel>(Expression<Func<TModel, bool>> predicate, UpstreamIntegrationOptions options = null) where TModel : IdentifiedData
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IdentifiedData Get(Type modelType, Guid key, Guid? versionKey, UpstreamIntegrationOptions options = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public TModel Get<TModel>(Guid key, Guid? versionKey, UpstreamIntegrationOptions options = null) where TModel : IdentifiedData
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public void Insert(IdentifiedData data)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Obsolete(IdentifiedData data, bool forceObsolete = false)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Update(IdentifiedData data, bool forceUpdate = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get a <see cref="IPrincipal"/> representing the authenticated device with the upstream
        /// </summary>
        public IPrincipal AuthenticateAsDevice()
        {
            try
            {
                var deviceCredentialSettings = m_configuration.Credentials.Single(o => o.CredentialType == UpstreamCredentialType.Device);
                var applicationCredentialSettings = m_configuration.Credentials.Single(o => o.CredentialType == UpstreamCredentialType.Application);
                if (deviceCredentialSettings == null || applicationCredentialSettings == null)
                {
                    throw new InvalidOperationException(ErrorMessages.UPSTREAM_NOT_CONFIGURED);
                }

                IPrincipal devicePrincipal = null;
                switch (deviceCredentialSettings.Conveyance)
                {
                    case UpstreamCredentialConveyance.Secret:
                    case UpstreamCredentialConveyance.Header:
                        var deviceIdentityProvider = ApplicationServiceContext.Current.GetService<IDeviceIdentityProviderService>();
                        devicePrincipal = deviceIdentityProvider.Authenticate(deviceCredentialSettings.CredentialName, deviceCredentialSettings.CredentialSecret, AuthenticationMethod.Local);
                        break;
                    case UpstreamCredentialConveyance.ClientCertificate:
                        var certificateIdentityProvider = ApplicationServiceContext.Current.GetService<ICertificateIdentityProvider>();
                        devicePrincipal = certificateIdentityProvider.Authenticate(deviceCredentialSettings.CertificateSecret.Certificate);
                        break;
                    default:
                        throw new InvalidOperationException(string.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, deviceCredentialSettings.Conveyance));
                }

                var applicationIdentityProvider = ApplicationServiceContext.Current.GetService<IApplicationIdentityProviderService>();
                return applicationIdentityProvider.Authenticate(applicationCredentialSettings.CredentialName, devicePrincipal);

            }
            catch (Exception e)
            {
                throw new SecurityException(m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), e);
            }
        }


        /// <summary>
        /// Harmonize the template identifiers
        /// </summary>
        public IHasTemplate HarmonizeTemplateId(IHasTemplate template)
        {
            if (template.Template != null &&
                !template.TemplateKey.HasValue)
            {
                if (!s_templateKeys.TryGetValue(template.Template.Mnemonic, out Guid retVal))
                {
                    using (AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
                    {
                        using (var client = new HdsiServiceClient(this.m_restClientFactory.GetRestClientFor(ServiceEndpointType.HealthDataService)))
                        {
                            var itm = client.Query<TemplateDefinition>(o => o.Mnemonic == template.Template.Mnemonic);
                            itm.Item.OfType<TemplateDefinition>().ToList().ForEach(o => s_templateKeys.TryAdd(o.Mnemonic, o.Key.Value));
                            return this.HarmonizeTemplateId(template);
                        }
                    }
                }
                template.TemplateKey = retVal;
            }
            return template;

        }

    }
}
