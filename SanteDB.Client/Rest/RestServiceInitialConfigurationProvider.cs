using RestSrvr.Attributes;
using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Interop;
using SanteDB.Core.Services;
using SanteDB.Rest.AMI;
using SanteDB.Rest.BIS;
using SanteDB.Rest.Common.Behavior;
using SanteDB.Rest.Common.Configuration;
using SanteDB.Rest.Common.Security;
using SanteDB.Rest.HDSI;
using SanteDB.Rest.OAuth;
using SanteDB.Rest.OAuth.Rest;
using SanteDB.Rest.WWW.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SanteDB.Client.Rest
{
    /// <summary>
    /// An implementation of an <see cref="IInitialConfigurationProvider"/> which sets up the default REST services
    /// for the SanteDB dCDR instance
    /// </summary>
    public class RestServiceInitialConfigurationProvider : IInitialConfigurationProvider
    {

        // API service providers
        private IDictionary<ServiceEndpointType, Type> m_apiServiceProviders = AppDomain.CurrentDomain.GetAllTypes().Where(t => t.GetCustomAttribute<ApiServiceProviderAttribute>() != null)
            .ToDictionary(o => o.GetCustomAttribute<ApiServiceProviderAttribute>().ServiceType, o => o);

        /// <summary>
        /// The binding application data
        /// </summary>
        public const string BINDING_BASE_DATA = "http.binding";

        /// <summary>
        /// Create a rest service configuration for the specified <paramref name="serviceEndpointType"/>
        /// </summary>
        /// <param name="serviceEndpointType">The type of service endpoint to create a configuration for</param>
        /// <param name="path">The base path of the endpoint</param>
        /// <param name="serviceBehaviors">The service behaviors</param>
        /// <param name="endpointBehaviors">The endpoint behaviors</param>
        /// <param name="configuration">The configuration to which the service should be added</param>
        /// <returns>The configuration</returns>
        private void AddRestServiceFor(SanteDBConfiguration configuration, ServiceEndpointType serviceEndpointType, String path, ICollection<RestServiceBehaviorConfiguration> serviceBehaviors, ICollection<RestEndpointBehaviorConfiguration> endpointBehaviors)
        {

            var restConfiguration = configuration.GetSection<RestConfigurationSection>();
            if (restConfiguration == null)
            {
                restConfiguration = new RestConfigurationSection();
                configuration.AddSection(restConfiguration);
            }
            var appConfiguration = configuration.GetSection<ApplicationServiceContextConfigurationSection>();

            if (!this.m_apiServiceProviders.TryGetValue(serviceEndpointType, out var serviceType))
            {
                return;
            }
            var serviceMetadata = serviceType?.GetCustomAttribute<ApiServiceProviderAttribute>();
            var serviceContract = serviceMetadata?.BehaviorType.GetInterfaces().FirstOrDefault(o => o.GetCustomAttribute<ServiceContractAttribute>() != null);
            var serviceContractMetadata = serviceMetadata.BehaviorType?.GetCustomAttribute<ServiceBehaviorAttribute>();
            if (serviceContractMetadata == null)
            {
                return;
            }

            // Default Configuration for BIS
            restConfiguration.Services.Add(new RestServiceConfiguration(typeof(BisServiceBehavior))
            {
                Behaviors = serviceBehaviors.ToList(),
                ConfigurationName = serviceContractMetadata.Name,
                Endpoints = new List<RestEndpointConfiguration>()
                        {
                            new RestEndpointConfiguration()
                            {
                                Address = path,
                                Behaviors = endpointBehaviors.ToList(),
                                Contract = serviceContract
                            }
                        }
            });

            if (!appConfiguration.ServiceProviders.Any(t => t.Type == serviceType))
            {
                appConfiguration.ServiceProviders.Add(new TypeReferenceConfiguration(serviceType));
            }
        }

        /// <inheritdoc/>
        public virtual SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration)
        {

            var bindingBase = new Uri(AppDomain.CurrentDomain.GetData(BINDING_BASE_DATA)?.ToString());
            if (bindingBase == null)
            {
                switch (hostContextType)
                {
                    case SanteDBHostType.Client:
                        bindingBase = new Uri("http://127.0.0.1:9200");
                        break;
                    case SanteDBHostType.Gateway:
                        bindingBase = new Uri("http://0.0.0.0:9200");
                        break;
                }
            }

            // Behaviors for a secured endpoint
            var webBehaviors = new List<RestServiceBehaviorConfiguration>()
            {
                new RestServiceBehaviorConfiguration(typeof(WebErrorBehavior))
            };

            var apiBehaviors = new List<RestServiceBehaviorConfiguration>()
            {
                new RestServiceBehaviorConfiguration(typeof(ErrorServiceBehavior)),
                new RestServiceBehaviorConfiguration(typeof(ServerMetadataServiceBehavior))
            };

            var oauthBehaviors = new List<RestServiceBehaviorConfiguration>(apiBehaviors) {
                new RestServiceBehaviorConfiguration(typeof(ClientCertificateAccessBehavior)),
                new RestServiceBehaviorConfiguration(typeof(ClientAuthorizationAccessBehavior))
            };

            apiBehaviors.Add(new RestServiceBehaviorConfiguration(typeof(TokenAuthorizationAccessBehavior)));
            webBehaviors.Add(new RestServiceBehaviorConfiguration(typeof(TokenAuthorizationAccessBehavior)));

            if (hostContextType == SanteDBHostType.Client)
            {
                apiBehaviors.Add(new RestServiceBehaviorConfiguration(typeof(WebMagicBehavior)));
                webBehaviors.Add(new RestServiceBehaviorConfiguration(typeof(WebMagicBehavior)));
            }

            var endpointBehaviors = new List<RestEndpointBehaviorConfiguration>()
            {
                new RestEndpointBehaviorConfiguration(typeof(AcceptLanguageEndpointBehavior)),
                new RestEndpointBehaviorConfiguration(typeof(SecurityPolicyEnforcementBehavior)),
                new RestEndpointBehaviorConfiguration(typeof(MessageDispatchFormatterBehavior)),
#if DEBUG
                new RestEndpointBehaviorConfiguration(typeof(MessageLoggingEndpointBehavior)),
#endif
                new RestEndpointBehaviorConfiguration(typeof(MessageCompressionEndpointBehavior)),
                new RestEndpointBehaviorConfiguration(typeof(SecurityPolicyEnforcementBehavior))
            };


            this.AddRestServiceFor(configuration, ServiceEndpointType.HealthDataService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/hdsi", apiBehaviors, endpointBehaviors);
            this.AddRestServiceFor(configuration, ServiceEndpointType.BusinessIntelligenceService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/bis", apiBehaviors, endpointBehaviors);
            this.AddRestServiceFor(configuration, ServiceEndpointType.ApplicationControlService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/app", webBehaviors, endpointBehaviors);
            this.AddRestServiceFor(configuration, ServiceEndpointType.AuthenticationService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/auth", oauthBehaviors, endpointBehaviors);
            this.AddRestServiceFor(configuration, ServiceEndpointType.WebUserInterfaceService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/", webBehaviors, endpointBehaviors);
            return configuration;

        }
    }
}
