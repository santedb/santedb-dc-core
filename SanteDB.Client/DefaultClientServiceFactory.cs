﻿using SanteDB.Client.Upstream.Management;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client
{
    /// <summary>
    /// A service factory that creates the default client services
    /// </summary>
    public class DefaultClientServiceFactory : IServiceFactory
    {
        private readonly Type[] m_serviceTypes = new Type[]
        {
            typeof(DefaultUpstreamManagementService),
            typeof(DefaultUpstreamAvailabilityProvider),
            typeof(DefaultUpstreamIntegrationService)
        };
        private readonly IServiceManager m_serviceManager;

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultClientServiceFactory(IServiceManager serviceManager)
        {
            this.m_serviceManager = serviceManager;
        }

        /// <inheritdoc/>
        public bool TryCreateService<TService>(out TService serviceInstance)
        {
            if(this.TryCreateService(typeof(TService), out var nonGenService))
            {
                serviceInstance = (TService)nonGenService;
                return true;
            }
            serviceInstance = default(TService);
            return false;
        }

        /// <inheritdoc/>
        public bool TryCreateService(Type serviceType, out object serviceInstance)
        {
            if(this.m_serviceTypes.Any(t=>serviceType.IsAssignableFrom(t)))
            {
                serviceInstance = this.m_serviceManager.CreateInjected(serviceType);
                return true;
            }
            serviceInstance = null;
            return false;
        }
    }
}
