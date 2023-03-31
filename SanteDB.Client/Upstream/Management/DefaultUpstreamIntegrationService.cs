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
 * Date: 2023-3-10
 */
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Applets.ViewModel.Json;
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
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
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
                if (credentialConfiguration.Conveyance == UpstreamCredentialConveyance.ClientCertificate) {
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
            ILocalizationService localizationService)
        {
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>();
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamManager = upstreamManagementService;
            this.m_localizationService = localizationService;
            this.m_networkInformationService = networkInformationService;
            this.m_serviceManager = serviceManager;


        }

        private IRepositoryService GetRepositoryService(Type modelType)
        {
            var repositorytype = typeof(IRepositoryService<>).MakeGenericType(modelType);

            return m_serviceManager.CreateInjected(repositorytype) as IRepositoryService;
        }

        private IQueryResultSet<IdentifiedData> Transform(IQueryResultSet qrs)
        {
            if (null == qrs)
            {
                return default;
            }

            if (qrs is IQueryResultSet<IdentifiedData> iqrsid)
            {
                return iqrsid;
            }

            var qrstype = qrs.GetType();
            
            if (qrstype.IsGenericType && qrstype.GetGenericTypeDefinition() == typeof(IQueryResultSet<>))
            {
                var modeltype = qrstype.GetGenericArguments()[0];

                if (!typeof(IdentifiedData).IsAssignableFrom(modeltype))
                {
                    throw new NotSupportedException($"Result set model type {modeltype.Name} is not derived from {nameof(IdentifiedData)}");
                }

                var transformertype = typeof(TransformQueryResultSet<,>).MakeGenericType(modeltype, typeof(IdentifiedData));

                var parm = Expression.Parameter(modeltype, "o");
                LambdaExpression converter = Expression.Lambda(
                    Expression.Convert(parm, typeof(IdentifiedData)),
                    parm
                    );

                var tqrs = (IQueryResultSet<IdentifiedData>)Activator.CreateInstance(transformertype, qrs, converter.Compile());

                return tqrs;
            }
            else
            {
                throw new NotSupportedException($"Cannot transform {qrstype.Name} into {typeof(IQueryResultSet<IdentifiedData>).ToString()}");
            }
            
        }

        /// <inheritdoc/>
        public IQueryResultSet<IdentifiedData> Find(Type modelType, NameValueCollection filter, UpstreamIntegrationOptions options = null)
        {
            return Transform(GetRepositoryService(modelType)?.Find(QueryExpressionParser.BuildLinqExpression(modelType, filter)));
        }

        /// <inheritdoc/>
        public IQueryResultSet<IdentifiedData> Find<TModel>(NameValueCollection filter, int offset, int? count, UpstreamIntegrationOptions options = null) where TModel : IdentifiedData
        {
            return new TransformQueryResultSet<TModel, IdentifiedData>(m_serviceManager.CreateInjected<IRepositoryService<TModel>>()?.Find(QueryExpressionParser.BuildLinqExpression<TModel>(filter)), tm => tm); ;
        }

        /// <inheritdoc/>
        public IQueryResultSet<IdentifiedData> Find<TModel>(Expression<Func<TModel, bool>> predicate, UpstreamIntegrationOptions options = null) where TModel : IdentifiedData
        {
            var qrs =  m_serviceManager.CreateInjected<IRepositoryService<TModel>>()?.Find(predicate);

            return new TransformQueryResultSet<TModel, IdentifiedData>(qrs, tm => tm);
        }

        /// <inheritdoc/>
        public IdentifiedData Get(Type modelType, Guid key, Guid? versionKey, UpstreamIntegrationOptions options = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public TModel Get<TModel>(Guid key, Guid? versionKey, UpstreamIntegrationOptions options = null) where TModel : IdentifiedData
        {
            if (null != versionKey)
            {
                return m_serviceManager.CreateInjected<IRepositoryService<TModel>>()?.Get(key, versionKey.Value);
            }
            else
            {
                return m_serviceManager.CreateInjected<IRepositoryService<TModel>>()?.Get(key);
            }
        }


        /// <inheritdoc/>
        public void Insert(IdentifiedData data)
        {
            if (null == data)
            {
                return;
            }

            GetRepositoryService(data.GetType())?.Insert(data);
        }

        /// <inheritdoc/>
        public void Obsolete(IdentifiedData data, bool forceObsolete = false)
        {
            if (null == data?.Key)
            {
                return;
            }

            GetRepositoryService(data.GetType())?.Delete(data.Key.Value);

            //TODO: Handle forceObsolete by getting dependent objects and resolving.
        }

        /// <inheritdoc/>
        public void Update(IdentifiedData data, bool forceUpdate = false)
        {
            GetRepositoryService(data.GetType())?.Save(data);

            //TODO: Handle forceUpdate by getting dependent objects and resolving.
        }

        /// <summary>
        /// Get a <see cref="IPrincipal"/> representing the authenticated device with the upstream
        /// </summary>
        public IPrincipal AuthenticateAsDevice()
        {
            try
            {

                if(this.m_devicePrincipal != null && this.m_devicePrincipal.ExpiresAt.AddMinutes(-2) > DateTimeOffset.Now)
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

                var applicationIdentityProvider = ApplicationServiceContext.Current.GetService<IApplicationIdentityProviderService>();
                this.m_devicePrincipal = applicationIdentityProvider.Authenticate(applicationCredentialSettings.CredentialName, devicePrincipal) as ITokenPrincipal;
                return this.m_devicePrincipal;
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
