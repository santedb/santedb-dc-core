/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */

using RestSrvr;
using RestSrvr.Attributes;
using RestSrvr.Bindings;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Interop;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Behaviors;
using SanteDB.DisconnectedClient.Ags.Configuration;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Metadata;
using SanteDB.DisconnectedClient.Ags.Services;
using SanteDB.Rest.AMI;
using SanteDB.Rest.BIS;
using SanteDB.Rest.Common.Behavior;
using SanteDB.Rest.Common.Behaviors;
using SanteDB.Rest.HDSI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Ags
{
    /// <summary>
    /// The Applet Gateway Service 
    /// </summary>
    /// <remarks>
    /// <para>The AGS service is responsible for starting the REST based services for the dCDR instance and is responsible
    /// for maintaining and managing these services through the life cycle including:</para>
    /// <list type="bullet">
    ///     <item><see href="https://help.santesuite.org/developers/service-apis/health-data-service-interface-hdsi">Health Data Service Interface</see></item>
    ///     <item><see href="https://help.santesuite.org/developers/service-apis/administration-management-interface-ami">Administration Management Interface</see></item>
    ///     <item><see href="https://help.santesuite.org/developers/service-apis/business-intelligence-services-bis">Business Intelligence Service</see></item>
    /// </list>
    /// </remarks>
    public class AgsService : IDaemonService, IRestServiceFactory
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Offline Application Gateway Service";

        // Backing for HDSI service
        private List<RestService> m_services = new List<RestService>();

        // Tracers
        private Tracer m_tracer = Tracer.GetTracer(typeof(AgsService));

        /// <summary>
        /// Determine whether the service is fully operational
        /// </summary>
        public bool IsRunning =>
            this.m_services.Count > 0 &&
            this.m_services.All(o => o.IsRunning);

        /// <summary>
        /// Fired when the handler is starting
        /// </summary>
        public event EventHandler Starting;

        /// <summary>
        /// Fired when the handler has started
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        /// Fired when the handler is stopping
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        /// Fired when the handler has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Gets the default AGS configuration
        /// </summary>
        /// <returns></returns>
        public static AgsConfigurationSection GetDefaultConfiguration()
        {
            // Behaviors for a secured endpoint
            var webBehaviors = new List<AgsBehaviorConfiguration>()
            {
                new AgsBehaviorConfiguration(typeof(AgsWebErrorHandlerServiceBehavior))
            };

            var apiBehaviors = new List<AgsBehaviorConfiguration>()
            {
                new AgsBehaviorConfiguration(typeof(AgsErrorHandlerServiceBehavior))
            };

            var endpointBehaviors = new List<AgsBehaviorConfiguration>()
            {
                new AgsBehaviorConfiguration(typeof(AcceptLanguageEndpointBehavior)),
                new AgsBehaviorConfiguration(typeof(SecurityPolicyHeadersBehavior)),
                new AgsBehaviorConfiguration(typeof(AgsSerializationEndpointBehavior)),
#if DEBUG
                new AgsBehaviorConfiguration(typeof(MessageLoggingEndpointBehavior)),
#endif
                new AgsBehaviorConfiguration(typeof(MessageCompressionEndpointBehavior))
            };

            return new AgsConfigurationSection()
            {
                Services = new List<AgsServiceConfiguration>()
                {
                    // Default Configuration for BIS
                    new AgsServiceConfiguration(typeof(BisServiceBehavior))
                    {
                        Behaviors = apiBehaviors,
                        Endpoints = new List<AgsEndpointConfiguration>()
                        {
                            new AgsEndpointConfiguration()
                            {
                                Address = "http://127.0.0.1:9200/bis",
                                Behaviors = endpointBehaviors,
                                Contract = typeof(IBisServiceContract)
                            }
                        }
                    },
                    // Default Configuration for HDSI
                    new AgsServiceConfiguration(typeof(HdsiServiceBehavior))
                    {
                        Behaviors = apiBehaviors,
                        Endpoints = new List<AgsEndpointConfiguration>()
                        {
                            new AgsEndpointConfiguration()
                            {
                                Address = "http://127.0.0.1:9200/hdsi",
                                Behaviors = endpointBehaviors,
                                Contract = typeof(IHdsiServiceContract)
                            }
                        }
                    },
                    // Default Configuration for AMI
                    new AgsServiceConfiguration(typeof(AmiServiceBehavior))
                    {
                        Behaviors = apiBehaviors,
                        Endpoints = new List<AgsEndpointConfiguration>()
                        {
                            new AgsEndpointConfiguration()
                            {
                                Address = "http://127.0.0.1:9200/ami",
                                Behaviors = endpointBehaviors,
                                Contract = typeof(IAmiServiceContract)
                            }
                        }
                    },
                    // Default Configuration for Security service
                    new AgsServiceConfiguration(typeof(AuthenticationServiceBehavior))
                    {
                        Behaviors = apiBehaviors,
                        Endpoints = new List<AgsEndpointConfiguration>()
                        {
                            new AgsEndpointConfiguration()
                            {
                                Address = "http://127.0.0.1:9200/auth",
                                Behaviors = endpointBehaviors,
                                Contract = typeof(IAuthenticationServiceContract)
                            }
                        }
                    },
                    // Default configuration for Application service
                    new AgsServiceConfiguration(typeof(ApplicationServiceBehavior))
                    {
                        Behaviors = apiBehaviors,
                        Endpoints = new List<AgsEndpointConfiguration>()
                        {
                            new AgsEndpointConfiguration()
                            {
                                Address = "http://127.0.0.1:9200/app",
                                Behaviors = endpointBehaviors,
                                Contract = typeof(IApplicationServiceContract)
                            }
                        }
                    },
                    new AgsServiceConfiguration(typeof(WwwServiceBehavior))
                    {
                        Behaviors = webBehaviors,
                        Endpoints = new List<AgsEndpointConfiguration>()
                        {
                            new AgsEndpointConfiguration()
                            {
                                Address = "http://127.0.0.1:9200/",
                                Behaviors = endpointBehaviors,
                                Contract = typeof(IWwwServiceContract)
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates a REST service instance
        /// </summary>
        private RestService CreateRestService(String prefix, Type behavior)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Starts this service handler
        /// </summary>
        public bool Start()
        {
            RemoteEndpointUtil.Current.AddEndpointProvider(this.GetRemoteEndpointInfo);
            this.Starting?.Invoke(this, EventArgs.Empty);

            // Start up each of the services
            foreach (var itm in ApplicationContext.Current.Configuration.GetSection<AgsConfigurationSection>().Services)
            {
                this.m_tracer.TraceInfo("Starting Application Gateway Service {0}..", itm.Name);
                // Service Behaviors
                RestService service = new RestService(itm.ServiceType);

                service.AddServiceBehavior(new AgsAuthorizationServiceBehavior());
                service.AddServiceBehavior(new AgsMagicServiceBehavior());
                service.AddServiceBehavior(new AgsPermissionPolicyBehavior(itm.ServiceType));

                foreach (var bhvr in itm.Behaviors)
                {
                    this.m_tracer.TraceVerbose("AGS Service {0} has behavior {1}", itm.Name, bhvr.XmlType);
                    service.AddServiceBehavior(this.CreateBehavior<IServiceBehavior>(bhvr));
                }
                // Endpoints
                foreach (var ep in itm.Endpoints)
                {
                    this.m_tracer.TraceInfo("\tEndpoint: {0}", ep.Address);
                    var serviceEndpoint = service.AddServiceEndpoint(new Uri(ep.Address), ep.Contract, new RestHttpBinding(false));
                    foreach (var bhvr in ep.Behaviors)
                    {
                        this.m_tracer.TraceVerbose("AGS Service {0} endpoint {1} has behavior {2}", itm.Name, ep.Address, bhvr.XmlType);
                        serviceEndpoint.AddEndpointBehavior(this.CreateBehavior<IEndpointBehavior>(bhvr));
                    }
                }

                // Add the specified object to the discovery processor
                ServiceEndpointType apiType = ServiceEndpointType.Other;
                if (typeof(IAmiServiceContract).IsAssignableFrom(itm.ServiceType))
                    apiType = ServiceEndpointType.AdministrationIntegrationService;
                else if (typeof(IHdsiServiceContract).IsAssignableFrom(itm.ServiceType))
                    apiType = ServiceEndpointType.HealthDataService;
                else if (typeof(IBisServiceContract).IsAssignableFrom(itm.ServiceType))
                    apiType = ServiceEndpointType.BusinessIntelligenceService;
                else if (typeof(IAuthenticationServiceContract).IsAssignableFrom(itm.ServiceType))
                    apiType = ServiceEndpointType.AuthenticationService;
                else if (typeof(IApplicationServiceContract).IsAssignableFrom(itm.ServiceType))
                    apiType = ServiceEndpointType.Other | ServiceEndpointType.AdministrationIntegrationService;
                ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(new ApiEndpointProviderShim(itm.ServiceType, apiType, itm.Endpoints.First().Address, (ServiceEndpointCapabilities)this.GetServiceCapabilities(service)));
                // Start the service
                this.m_services.Add(service);
                service.Start();
            }

            this.Started?.Invoke(this, EventArgs.Empty);
            return this.IsRunning;
        }

        /// <summary>
        /// Create the behavior of type T
        /// </summary>
        private TBehavior CreateBehavior<TBehavior>(AgsBehaviorConfiguration bhvr)
        {
            if (!typeof(TBehavior).IsAssignableFrom(bhvr.Type))
                throw new ArgumentException($"Behavior {bhvr.Type.FullName} does not implement {typeof(TBehavior).FullName}");
            if (bhvr.Configuration != null)
                return (TBehavior)Activator.CreateInstance(bhvr.Type, bhvr.Configuration);
            else
                return (TBehavior)Activator.CreateInstance(bhvr.Type);
        }

        /// <summary>
        /// Stops this service handler
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            foreach (var itm in this.m_services)
            {
                this.m_tracer.TraceInfo("Stopping {0}...", itm.Name);
                itm.Stop();
            }
            this.m_services.Clear();

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return !this.IsRunning;
        }

        /// <summary>
        /// Get capabilities
        /// </summary>
        public int GetServiceCapabilities(RestService me)
        {
            var retVal = ServiceEndpointCapabilities.None;
            // Any of the capabilities are for security?
            if (me.ServiceBehaviors.OfType<AgsAuthorizationServiceBehavior>().Any())
                retVal |= ServiceEndpointCapabilities.BearerAuth;
            if (me.Endpoints.Any(e => e.Behaviors.OfType<MessageCompressionEndpointBehavior>().Any()))
                retVal |= ServiceEndpointCapabilities.Compression;
            if (me.Endpoints.Any(e => e.Behaviors.OfType<CorsEndpointBehavior>().Any()))
                retVal |= ServiceEndpointCapabilities.Cors;
            if (me.Endpoints.Any(e => e.Behaviors.OfType<MessageDispatchFormatterBehavior>().Any()))
                retVal |= ServiceEndpointCapabilities.ViewModel;
            return (int)retVal;
        }

        /// <summary>
        /// Create the rest service
        /// </summary>
        public RestService CreateService(Type serviceType)
        {
            try
            {
                // Get the configuration
                var configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<AgsConfigurationSection>();
                var sname = serviceType.GetCustomAttribute<ServiceBehaviorAttribute>()?.Name ?? serviceType.FullName;
                var config = configuration.Services.FirstOrDefault(o => o.Name == sname);
                if (config == null)
                    throw new InvalidOperationException($"Cannot find configuration for {sname}");
                var retVal = new RestService(serviceType);
                foreach (var bhvr in config.Behaviors)
                    retVal.AddServiceBehavior(
                        bhvr.Configuration == null ?
                        Activator.CreateInstance(bhvr.Type) as IServiceBehavior :
                        Activator.CreateInstance(bhvr.Type, bhvr.Configuration) as IServiceBehavior);

                var demandPolicy = new AgsPermissionPolicyBehavior(serviceType);

                foreach (var ep in config.Endpoints)
                {
                    var se = retVal.AddServiceEndpoint(new Uri(ep.Address), ep.Contract, new RestHttpBinding(false));
                    foreach (var bhvr in ep.Behaviors)
                    {
                        se.AddEndpointBehavior(
                            bhvr.Configuration == null ?
                            Activator.CreateInstance(bhvr.Type) as IEndpointBehavior :
                            Activator.CreateInstance(bhvr.Type, bhvr.Configuration) as IEndpointBehavior);
                        se.AddEndpointBehavior(demandPolicy);
                    }
                }
                return retVal;
            }
            catch (Exception e)
            {
                Tracer.GetTracer(typeof(AgsService)).TraceError("Could not start {0} : {1}", serviceType.FullName, e);
                throw new Exception($"Could not start {serviceType.FullName}", e);
            }
        }

        /// <summary>
        /// Retrieve the remote endpoint information
        /// </summary>
        /// <returns></returns>
        public RemoteEndpointInfo GetRemoteEndpointInfo()
        {
            if (RestOperationContext.Current == null) return null;
            else
            {
                var fwdHeader = RestOperationContext.Current?.IncomingRequest.Headers["X-Forwarded-For"];
                var realIpHeader = RestOperationContext.Current.IncomingRequest.Headers["X-Real-IP"];
                return new RemoteEndpointInfo()
                {
                    OriginalRequestUrl = RestOperationContext.Current?.IncomingRequest.Url.ToString(),
                    ForwardInformation = fwdHeader,
                    RemoteAddress = realIpHeader ?? RestOperationContext.Current?.IncomingRequest.RemoteEndPoint.Address.ToString(),
                    CorrelationToken = RestOperationContext.Current?.Data["uuid"]?.ToString()
                };
            }
        }
    }
}