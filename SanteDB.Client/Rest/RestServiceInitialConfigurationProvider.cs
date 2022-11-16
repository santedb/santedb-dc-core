using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Client.Configuration;
using SanteDB.Client.OAuth;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Certs;
using SanteDB.Core.Services;
using SanteDB.Messaging.HDSI.Wcf;
using SanteDB.Rest.AMI;
using SanteDB.Rest.BIS;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Behavior;
using SanteDB.Rest.Common.Behaviors;
using SanteDB.Rest.Common.Configuration;
using SanteDB.Rest.Common.Configuration.Interop;
using SanteDB.Rest.Common.Security;
using SanteDB.Rest.HDSI;
using SanteDB.Rest.OAuth;
using SanteDB.Rest.OAuth.Rest;
using SanteDB.Rest.WWW.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SanteDB.Client.Rest
{
    /// <summary>
    /// An implementation of an <see cref="IInitialConfigurationProvider"/> which sets up the default REST services
    /// for the SanteDB dCDR instance
    /// </summary>
    public class RestServiceInitialConfigurationProvider : IInitialConfigurationProvider
    {

        /// <inheritdoc/>
        public int Order => 0;

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
        private RestServiceConfiguration AddRestServiceFor(SanteDBConfiguration configuration, ServiceEndpointType serviceEndpointType, String path, ICollection<RestServiceBehaviorConfiguration> serviceBehaviors, ICollection<RestEndpointBehaviorConfiguration> endpointBehaviors)
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
                return null;
            }
            var serviceMetadata = serviceType?.GetCustomAttribute<ApiServiceProviderAttribute>();
            var serviceContract = serviceMetadata?.BehaviorType.GetInterfaces().FirstOrDefault(o => o.GetCustomAttribute<ServiceContractAttribute>() != null);
            var serviceContractMetadata = serviceMetadata.BehaviorType?.GetCustomAttribute<ServiceBehaviorAttribute>();
            if (serviceContractMetadata == null)
            {
                return null;
            }

            // Default Configuration for service
            var svc = new RestServiceConfiguration(serviceMetadata.BehaviorType)
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
            };

            if (!appConfiguration.ServiceProviders.Any(t => t.Type == serviceType))
            {
                appConfiguration.ServiceProviders.Add(new TypeReferenceConfiguration(serviceType));
            }

            if (!restConfiguration.Services.Any(o => o.ConfigurationName == svc.ConfigurationName))
            {
                restConfiguration.Services.Add(svc);
            }
            return svc;
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
                new RestServiceBehaviorConfiguration(typeof(WebErrorBehavior)),
                new RestServiceBehaviorConfiguration(typeof(CookieAuthenticationBehavior)),
                new RestServiceBehaviorConfiguration(typeof(TokenAuthorizationAccessBehavior))
            };

            var apiBehaviors = new List<RestServiceBehaviorConfiguration>()
            {
                new RestServiceBehaviorConfiguration(typeof(ErrorServiceBehavior)),
                new RestServiceBehaviorConfiguration(typeof(CookieAuthenticationBehavior)),
                new RestServiceBehaviorConfiguration(typeof(TokenAuthorizationAccessBehavior))
            };

            var oauthBehaviors = new List<RestServiceBehaviorConfiguration>(apiBehaviors) {
            };

            if (hostContextType == SanteDBHostType.Client)
            {
                apiBehaviors.Add(new RestServiceBehaviorConfiguration(typeof(WebMagicBehavior)));
                webBehaviors.Add(new RestServiceBehaviorConfiguration(typeof(WebMagicBehavior)));
                oauthBehaviors.Add(new RestServiceBehaviorConfiguration(typeof(WebMagicBehavior)));
            }


            var endpointBehaviors = new List<RestEndpointBehaviorConfiguration>()
            {
                new RestEndpointBehaviorConfiguration(typeof(AcceptLanguageEndpointBehavior)),
                new RestEndpointBehaviorConfiguration(typeof(MessageDispatchFormatterBehavior)),
#if DEBUG
                new RestEndpointBehaviorConfiguration(typeof(MessageLoggingEndpointBehavior)),
#endif
                new RestEndpointBehaviorConfiguration(typeof(MessageCompressionEndpointBehavior)),
                new RestEndpointBehaviorConfiguration(typeof(ServerMetadataServiceBehavior)),
                new RestEndpointBehaviorConfiguration(typeof(SecurityPolicyHeadersBehavior))
            };

            var oauth = this.AddRestServiceFor(configuration, ServiceEndpointType.AuthenticationService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/auth/", oauthBehaviors, endpointBehaviors);
            var hdsi = this.AddRestServiceFor(configuration, ServiceEndpointType.HealthDataService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/hdsi/", apiBehaviors, endpointBehaviors);
            var ami = this.AddRestServiceFor(configuration, ServiceEndpointType.AdministrationIntegrationService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/ami/", apiBehaviors, endpointBehaviors);
            var bis = this.AddRestServiceFor(configuration, ServiceEndpointType.BusinessIntelligenceService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/bis/", apiBehaviors, endpointBehaviors);
            this.AddRestServiceFor(configuration, ServiceEndpointType.ApplicationControlService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/app/", webBehaviors, endpointBehaviors);
            this.AddRestServiceFor(configuration, ServiceEndpointType.WebUserInterfaceService, $"{bindingBase.Scheme}://{bindingBase.Host}:{bindingBase.Port}/", webBehaviors, endpointBehaviors);

            oauth.ServiceType = typeof(ClientOAuthServiceBehavior);
            oauth.Endpoints.ForEach(o => o.Contract = typeof(IClientOAuthServiceContract));
            hdsi.ServiceType = typeof(UpstreamHdsiServiceBehavior);
            ami.ServiceType = typeof(UpstreamAmiServiceBehavior);
            bis.ServiceType = typeof(UpstreamBisServiceBehavior);

            var appConfiguration = configuration.GetSection<ApplicationServiceContextConfigurationSection>();

            if (!appConfiguration.ServiceProviders.Any(s => typeof(IRestServiceFactory).IsAssignableFrom(s.Type)))
            {
                appConfiguration.ServiceProviders.Add(new TypeReferenceConfiguration(typeof(RestServiceFactory)));
            }

            // Are we working on SSL?
            if (bindingBase.Scheme == "https")
            {
                var appService = appConfiguration.ServiceProviders.Find(o => typeof(ICertificateGeneratorService).IsAssignableFrom(o.Type));
                RestDebugCertificateInstallation.InstallDebuggerCertificate(bindingBase, Activator.CreateInstance(appService.Type) as ICertificateGeneratorService);
            }

            return configuration;

        }
    }
}
