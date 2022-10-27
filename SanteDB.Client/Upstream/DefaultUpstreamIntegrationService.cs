using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream
{
    /// <summary>
    /// An upstream integration service
    /// </summary>
    public class DefaultUpstreamIntegrationService : IUpstreamIntegrationService
    {
        private readonly IRestClientFactory m_restClientFactory;
        private readonly UpstreamConfigurationSection m_configuration;
        private readonly IDeviceIdentityProviderService m_deviceIdentityProvider;
        private readonly ICertificateIdentityProvider m_certificateIdentityProvider;
        private readonly ILocalizationService m_localizationService;
        private readonly IApplicationIdentityProviderService m_applicationIdentityProvider;
        private readonly ConfiguredUpstreamRealmSettings m_upstreamSettings;

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
        /// <param name="restClientFactory">Rest client factory for creating rest clients</param>
        /// <param name="configurationManager">The configuration manager for fetching configuration</param>
        /// <param name="deviceIdentityProvider">Device identity provider for authenticating this device</param>
        /// <param name="certificateIdentityProvider">The certificate identity provider for authenticating this device with a certificate</param>
        public DefaultUpstreamIntegrationService(IRestClientFactory restClientFactory,
            IConfigurationManager configurationManager,
            IDeviceIdentityProviderService deviceIdentityProvider,
            IApplicationIdentityProviderService applicationIdentityProvider,
            ICertificateIdentityProvider certificateIdentityProvider,
            ILocalizationService localizationService)
        {
            this.m_restClientFactory = restClientFactory;
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>();
            this.m_deviceIdentityProvider = deviceIdentityProvider;
            this.m_certificateIdentityProvider = certificateIdentityProvider;
            this.m_localizationService = localizationService;
            this.m_applicationIdentityProvider = applicationIdentityProvider;

            this.m_upstreamSettings = new ConfiguredUpstreamRealmSettings(this.m_configuration);
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
        public IUpstreamRealmSettings GetSettings() => this.m_upstreamSettings;

        /// <inheritdoc/>
        public TimeSpan GetTimeDrift()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Insert(IdentifiedData data)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public bool IsAvailable()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Join(IUpstreamRealmSettings targetRealm)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Obsolete(IdentifiedData data, bool forceObsolete = false)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void UnJoin()
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
                var deviceCredentialSettings = this.m_configuration.Credentials.Single(o => o.CredentialType == UpstreamCredentialType.Device);
                var applicationCredentialSettings = this.m_configuration.Credentials.Single(o => o.CredentialType == UpstreamCredentialType.Application);

                IPrincipal devicePrincipal = null;
                switch (deviceCredentialSettings.Conveyance)
                {
                    case UpstreamCredentialConveyance.Secret:
                    case UpstreamCredentialConveyance.Header:
                        devicePrincipal = this.m_deviceIdentityProvider.Authenticate(deviceCredentialSettings.CredentialName, deviceCredentialSettings.CredentialSecret);
                        break;
                    case UpstreamCredentialConveyance.ClientCertificate:
                        devicePrincipal = this.m_certificateIdentityProvider.Authenticate(deviceCredentialSettings.CertificateSecret.Certificate);
                        break;
                    default:
                        throw new InvalidOperationException(String.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, deviceCredentialSettings.Conveyance));
                }

                return this.m_applicationIdentityProvider.Authenticate(applicationCredentialSettings.CredentialName, devicePrincipal);
            
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), e);
            }
        }
    }
}
