/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Http;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.HDSI.Client;
using SanteDB.Rest.Common;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// An upstream integration service
    /// </summary>
    public class DefaultUpstreamIntegrationService : IUpstreamIntegrationService
    {

        /// <summary>
        /// A certificate principal
        /// </summary>
        private class CertificatePrincipal : SanteDBClaimsPrincipal, ICertificatePrincipal
        {
            /// <summary>
            /// Create a new basic header for this 
            /// </summary>
            public CertificatePrincipal(UpstreamCredentialConfiguration credentialConfiguration)
            {
                if (credentialConfiguration.Conveyance != UpstreamCredentialConveyance.ClientCertificate)
                {
                    throw new ArgumentOutOfRangeException(nameof(credentialConfiguration));
                }
                this.AddIdentity(new SanteDBClaimsIdentity(credentialConfiguration.CredentialName, true, "NONE"));
                this.AuthenticationCertificate = credentialConfiguration.CertificateSecret.Certificate;
            }

            /// <inheritdoc/>
            public X509Certificate2 AuthenticationCertificate { get; }
        }

        /// <summary>
        /// Represents a device principal which is used to represent basic authentication context information
        /// </summary>
        private class HttpBasicTokenPrincipal : SanteDBClaimsPrincipal, ITokenPrincipal
        {

            /// <summary>
            /// Create a new basic header for this 
            /// </summary>
            public HttpBasicTokenPrincipal(UpstreamCredentialConfiguration credentialConfiguration)
            {
                if (credentialConfiguration.Conveyance == UpstreamCredentialConveyance.ClientCertificate)
                {
                    throw new ArgumentOutOfRangeException(nameof(credentialConfiguration));
                }
                this.AddIdentity(new SanteDBClaimsIdentity(credentialConfiguration.CredentialName, true, "NONE"));
                this.TokenType = "BASIC";
                this.AccessToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentialConfiguration.CredentialName}:{credentialConfiguration.CredentialSecret}"));
                this.ExpiresAt = DateTimeOffset.Now.AddSeconds(10);
            }

            /// <inheritdoc/>
            public string AccessToken { get; }

            /// <inheritdoc/>
            public string TokenType { get; }

            /// <inheritdoc/>
            public DateTimeOffset ExpiresAt { get; }

            /// <inheritdoc/>
            public string IdentityToken => null;

            /// <inheritdoc/>
            public string RefreshToken => null;

        }

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(DefaultUpstreamIntegrationService));
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IUpstreamManagementService m_upstreamManager;
        private readonly UpstreamConfigurationSection m_configuration;
        private readonly ILocalizationService m_localizationService;
        private readonly INetworkInformationService m_networkInformationService;
        private readonly IServiceManager m_serviceManager;
        private readonly IAdhocCacheService m_adhocCache;
        private ConcurrentDictionary<String, Guid> s_templateKeys = new ConcurrentDictionary<string, Guid>();
        private ITokenPrincipal m_devicePrincipal;

        /// <inheritdoc/>
        public string ServiceName => "Upstream Data Provider";

#pragma warning disable CS0067
        /// <inheritdoc/>
        public event EventHandler<RestResponseEventArgs> Responding;
        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;
        /// <inheritdoc/>
        public event EventHandler<UpstreamIntegrationResultEventArgs> Responded;
#pragma warning restore

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUpstreamIntegrationService(IRestClientFactory restClientFactory,
            INetworkInformationService networkInformationService,
            IConfigurationManager configurationManager,
            IServiceManager serviceManager,
            IUpstreamManagementService upstreamManagementService,
            IAdhocCacheService adhocCacheService,
            ILocalizationService localizationService)
        {
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>();
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamManager = upstreamManagementService;
            this.m_localizationService = localizationService;
            this.m_networkInformationService = networkInformationService;
            this.m_serviceManager = serviceManager;
            this.m_adhocCache = adhocCacheService;
        }

        private IRepositoryService GetRepositoryService(Type modelType)
        {
            var repositorytype = typeof(IRepositoryService<>).MakeGenericType(modelType);

            return m_serviceManager.CreateInjected(repositorytype) as IRepositoryService;
        }

        /// <inheritdoc/>
        public IResourceCollection Query(Type modelType, Expression filter, UpstreamIntegrationQueryControlOptions queryControl)
        {
            try
            {
                var filterType = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(modelType, typeof(bool)));
                var method = this.GetType().GetGenericMethod(nameof(Query), new[] { modelType }, new[] { filterType, typeof(UpstreamIntegrationQueryControlOptions) });
                return method.Invoke(this, new object[] { filter, queryControl }) as IResourceCollection;
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }
        }

        /// <inheritdoc/>
        public IResourceCollection Query<TModel>(Expression<Func<TModel, bool>> predicate, UpstreamIntegrationQueryControlOptions queryControl) where TModel : IdentifiedData, new()
        {
            var upstreamService = UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint<TModel>();
            using (var authenticationContext = AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
            using (var client = this.m_restClientFactory.GetRestClientFor(upstreamService))
            {
                var query = QueryExpressionBuilder.BuildQuery(predicate);
                query.Add(QueryControlParameterNames.HttpCountParameterName, queryControl.Count.ToString());
                query.Add(QueryControlParameterNames.HttpOffsetParameterName, queryControl.Offset.ToString());
                query.Add(QueryControlParameterNames.HttpQueryStateParameterName, queryControl.QueryId.ToString());
                client.Credentials = new UpstreamPrincipalCredentials(AuthenticationContext.Current.Principal);

                client.Requesting += (o, e) =>
                {
                    if (queryControl?.IfModifiedSince.HasValue == true)
                        e.AdditionalHeaders[HttpRequestHeader.IfModifiedSince] = queryControl?.IfModifiedSince.Value.ToString();
                    else if (!String.IsNullOrEmpty(queryControl?.IfNoneMatch))
                        e.AdditionalHeaders[HttpRequestHeader.IfNoneMatch] = queryControl?.IfNoneMatch;

                    if (queryControl.IncludeRelatedInformation)
                    {
                        e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.IncludeRelatedObjectsHeader, "true");
                    }
                };
                client.Responding += (o, e) => this.Responding?.Invoke(o, e);

                if (queryControl.Timeout.HasValue)
                {
                    client.SetTimeout(queryControl.Timeout.Value);
                }

                switch (upstreamService)
                {
                    case ServiceEndpointType.HealthDataService:
                        return client.Get<Bundle>($"/{typeof(TModel).GetSerializationName()}", query);
                    case ServiceEndpointType.AdministrationIntegrationService:
                        return client.Get<AmiCollection>($"/{typeof(TModel).GetSerializationName()}", query);
                    default:
                        throw new InvalidOperationException(ErrorMessages.SERVICE_NOT_FOUND);
                }
            }
        }

        /// <inheritdoc/>
        public IdentifiedData Get(Type modelType, Guid key, Guid? versionKey, UpstreamIntegrationQueryControlOptions options = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public TModel Get<TModel>(Guid key, Guid? versionKey, UpstreamIntegrationQueryControlOptions options = null) where TModel : IdentifiedData
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

                if (this.m_devicePrincipal != null && this.m_devicePrincipal.ExpiresAt.AddMinutes(-2) > DateTimeOffset.Now)
                {
                    return this.m_devicePrincipal;
                }

                var deviceCredentialSettings = m_configuration.Credentials.Single(o => o.CredentialType == UpstreamCredentialType.Device);
                var applicationCredentialSettings = m_configuration.Credentials.Single(o => o.CredentialType == UpstreamCredentialType.Application);
                if (deviceCredentialSettings == null || applicationCredentialSettings == null)
                {
                    throw new InvalidOperationException(ErrorMessages.UPSTREAM_NOT_CONFIGURED);
                }

                IPrincipal devicePrincipal = null;
                switch (deviceCredentialSettings.Conveyance)
                {
                    case UpstreamCredentialConveyance.Header:
                    case UpstreamCredentialConveyance.Secret:
                        devicePrincipal = new HttpBasicTokenPrincipal(deviceCredentialSettings);
                        break;
                    case UpstreamCredentialConveyance.ClientCertificate:
                        devicePrincipal = new CertificatePrincipal(deviceCredentialSettings);
                        break;
                    default:
                        throw new InvalidOperationException(string.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, deviceCredentialSettings.Conveyance));
                }

                var applicationIdentityProvider = ApplicationServiceContext.Current.GetService<IUpstreamServiceProvider<IApplicationIdentityProviderService>>();
                this.m_devicePrincipal = applicationIdentityProvider.UpstreamProvider.Authenticate(applicationCredentialSettings.CredentialName, devicePrincipal) as ITokenPrincipal;
                return this.m_devicePrincipal;
            }
            catch (Exception e)
            {
                throw new SecurityException(m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), e);
            }
        }



        /// <summary>
        /// Get the upstream template keys
        /// </summary>
        private Guid GetUpstreamTemplateKey(TemplateDefinition template)
        {
            using (AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
            {
                using (var client = new HdsiServiceClient(this.m_restClientFactory.GetRestClientFor(ServiceEndpointType.HealthDataService)))
                {

                    // Check the cache first
                    TemplateDefinition[] cached = null;
                    if (this.m_adhocCache?.TryGet("server.template.key", out cached) != true)
                    {
                        cached = client.Query<TemplateDefinition>(e => e.ObsoletionTime == null).Item.OfType<TemplateDefinition>().ToArray();
                        this.m_adhocCache?.Add("server.template.key", cached);
                    }

                    var serverTemplate = cached.FirstOrDefault(o => o.Mnemonic == template.Mnemonic);
                    if (null == serverTemplate)
                    {
                        serverTemplate = template;
                        return client.Create(serverTemplate).Key.Value;
                    }
                    else
                    {
                        return serverTemplate.Key.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Map an upstream template key to the local
        /// </summary>
        private IdentifiedData HarmonizeTemplateId(IdentifiedData data)
        {
            switch (data)
            {
                case Bundle bdl:
                    bdl.Item.ForEach(r => this.HarmonizeTemplateId(r));
                    return bdl;
                case TemplateDefinition td:
                    GetUpstreamTemplateKey(td); //Will force the insert to upstream.
                    data.Key = td.Key;
                    return data;
                case IHasTemplate iht:
                    iht.TemplateKey = GetUpstreamTemplateKey(data.LoadProperty<TemplateDefinition>(nameof(IHasTemplate.Template)));
                    return data;
                default:
                    return data;
            }
        }

        /// <inheritdoc/>
        IHasTemplate IUpstreamIntegrationService.HarmonizeTemplateId(IHasTemplate iht) => this.HarmonizeTemplateId(iht as IdentifiedData) as IHasTemplate;
    }
}
