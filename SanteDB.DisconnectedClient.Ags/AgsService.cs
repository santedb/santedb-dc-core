﻿using RestSrvr;
using RestSrvr.Bindings;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Ags.Behaviors;
using SanteDB.DisconnectedClient.Ags.Configuration;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.Rest.AMI;
using SanteDB.Rest.HDSI;
using SanteDB.Rest.RISI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags
{
    /// <summary>
    /// Represents the Applet Gateway Service
    /// </summary>
    public class AgsService : IDaemonService
    {
        // Backing for HDSI service
        private List<RestService> m_services = new List<RestService>();

        // Tracers
        private Tracer m_tracer = Tracer.GetTracer(typeof(AgsService));

        /// <summary>
        /// Determine whether the service is fully operational
        /// </summary>
        public bool IsRunning =>
            this.m_services.Count > 0 &&
            this.m_services.All(o=>o.IsRunning);

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
            var securedBehaviors = new List<AgsBehaviorConfiguration>()
            {
                new AgsBehaviorConfiguration(typeof(AgsAuthorizationServiceBehavior)),
            };

            var endpointBehaviors = new List<AgsBehaviorConfiguration>()
            {
                new AgsBehaviorConfiguration(typeof(AgsSerializationEndpointBehavior)),
#if DEBUG
                new AgsBehaviorConfiguration(typeof(AgsMessageLoggingEndpointBehavior)),
#endif
                new AgsBehaviorConfiguration(typeof(AgsCompressionEndpointBehavior))
            };

            return new AgsConfigurationSection()
            {
                Services = new List<AgsServiceConfiguration>()
                {
                    // Default Configuration for HDSI
                    new AgsServiceConfiguration(typeof(HdsiServiceBehavior))
                    {
                        Behaviors = securedBehaviors,
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
                        Behaviors = securedBehaviors,
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
                    // Default Configuration for Report Services
                    new AgsServiceConfiguration(typeof(RisiServiceBehavior))
                    {
                        Behaviors = securedBehaviors,
                        Endpoints = new List<AgsEndpointConfiguration>()
                        {
                            new AgsEndpointConfiguration()
                            {
                                Address = "http://127.0.0.1:9200/risi",
                                Behaviors = endpointBehaviors,
                                Contract = typeof(IRisiServiceContract)
                            }
                        }
                    },
                    // Default Configuration for Security service
                    new AgsServiceConfiguration(typeof(AuthenticationServiceBehavior))
                    {
                        Behaviors = securedBehaviors,
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
                        Behaviors = securedBehaviors,
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
            this.Starting?.Invoke(this, EventArgs.Empty);

            // Start up each of the services
            foreach(var itm in ApplicationContext.Current.Configuration.GetSection<AgsConfigurationSection>().Services)
            {
                this.m_tracer.TraceInfo("Starting Application Gateway Service {0}..", itm.Name);
                // Service Behaviors
                RestService service = new RestService(itm.ServiceType);

                service.AddServiceBehavior(new AgsMagicServiceBehavior());
                service.AddServiceBehavior(new AgsErrorHandlerServiceBehavior());
                service.AddServiceBehavior(new AgsPermissionPolicyBehavior());
                foreach (var bhvr in itm.Behaviors)
                {
                    this.m_tracer.TraceVerbose("AGS Service {0} has behavior {1}", itm.Name, bhvr.XmlType);
                    service.AddServiceBehavior(this.CreateBehavior<IServiceBehavior>(bhvr));
                }
                // Endpoints
                foreach(var ep in itm.Endpoints)
                {
                    this.m_tracer.TraceInfo("\tEndpoint: {0}", ep.Address);
                    var serviceEndpoint = service.AddServiceEndpoint(new Uri(ep.Address), ep.Contract, new RestHttpBinding());
                    foreach (var bhvr in ep.Behaviors)
                    {
                        this.m_tracer.TraceVerbose("AGS Service {0} endpoint {1} has behavior {2}", itm.Name, ep.Address, bhvr.XmlType);
                        serviceEndpoint.AddEndpointBehavior(this.CreateBehavior<IEndpointBehavior>(bhvr));
                    }
                }

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

            foreach(var itm in this.m_services)
            {
                this.m_tracer.TraceInfo("Stopping {0}...", itm.Name);
                itm.Stop();
            }
            this.m_services.Clear();

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return !this.IsRunning;
        }
    }
}
