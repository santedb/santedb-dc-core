using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// Upstream repository factory
    /// </summary>
    public class UpstreamRepositoryFactory : IServiceFactory
    {

        private readonly Type[] m_upstreamServices = new Type[]
        {
            typeof(UpstreamPolicyInformationService),
            typeof(UpstreamSecurityChallengeProvider)
        };
        private readonly IServiceManager m_serviceManager;
        private IUpstreamIntegrationService m_upstreamIntegration;

        /// <summary>
        /// Constructor
        /// </summary>
        public UpstreamRepositoryFactory(IServiceManager serviceManager,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamIntegrationService upstreamIntegrationService = null)
        {
            this.m_serviceManager = serviceManager;
            this.m_upstreamIntegration = upstreamIntegrationService;
            upstreamManagementService.RealmChanged += (o, e) => this.m_upstreamIntegration = e.UpstreamIntegrationService;
        }

        /// <inheritdoc/>
        public bool TryCreateService<TService>(out TService serviceInstance)
        {
            if(this.TryCreateService(typeof(TService), out var inner))
            {
                serviceInstance = (TService)inner;
                return true;
            }
            serviceInstance = default(TService);
            return false;

        }

        /// <inheritdoc/>
        public bool TryCreateService(Type serviceType, out object serviceInstance)
        {
            // Not configured
            if(this.m_upstreamIntegration == null)
            {
                serviceInstance = false;
                return false;
            }

            var serviceCandidate = this.m_upstreamServices.FirstOrDefault(o => serviceType.IsAssignableFrom(o));
            if(serviceCandidate != null)
            {
                serviceInstance = this.m_serviceManager.CreateInjected(serviceCandidate);
                return true;
            }
            else if(serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IRepositoryService<>))
            {
                var instance = this.m_upstreamIntegration.GetUpstreamRepository(serviceType.GenericTypeArguments[0]);
                serviceInstance = instance;
                return instance != null;
            }
            serviceInstance = null;
            return false;
        }
    }
}
