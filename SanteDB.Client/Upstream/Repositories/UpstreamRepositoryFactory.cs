using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.Serialization;
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
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IUpstreamManagementService m_upstreamManagementService;
        private IDictionary<Type, IRepositoryService> m_serviceRepositories;

        /// <summary>
        /// Constructor
        /// </summary>
        public UpstreamRepositoryFactory(IServiceManager serviceManager,
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService)
        {
            this.m_serviceManager = serviceManager;
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamManagementService = upstreamManagementService;
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
            if(!this.m_upstreamManagementService.IsConfigured())
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
                var instance = this.GetUpstreamRepository(serviceType.GenericTypeArguments[0]);
                serviceInstance = instance;
                return instance != null;
            }
            serviceInstance = null;
            return false;
        }


        /// <summary>
        /// Get upstream repository for the specified type
        /// </summary>
        public IRepositoryService GetUpstreamRepository(Type forType)
        {
            if (this.m_serviceRepositories == null)
            {
                try
                {
                    var tEndpointMap = new Dictionary<Type, IRepositoryService>();
                    var serializationBinder = new ModelSerializationBinder();
                    using (var hdsiClient = this.m_restClientFactory.GetRestClientFor(ServiceEndpointType.HealthDataService))
                    {
                        hdsiClient.Options<ServiceOptions>("/").Resources.ForEach(o => tEndpointMap.Add(o.ResourceType, this.m_serviceManager.CreateInjected(typeof(HdsiUpstreamRepository<>).MakeGenericType(o.ResourceType)) as IRepositoryService));
                    }
                    using (var amiClient = this.m_restClientFactory.GetRestClientFor(ServiceEndpointType.AdministrationIntegrationService))
                    {
                        amiClient.Options<ServiceOptions>("/").Resources.ForEach(o => tEndpointMap.Add(o.ResourceType, this.m_serviceManager.CreateInjected(typeof(AmiUpstreamRepository<>).MakeGenericType(o.ResourceType)) as IRepositoryService));
                    }
                    this.m_serviceRepositories = tEndpointMap;
                }
                catch (Exception e)
                {
                    return null;
                }
            }

            if (this.m_serviceRepositories.TryGetValue(forType, out var repositoryService))
            {
                return repositoryService;
            }
            else
            {
                return null;
            }
        }
    }
}
