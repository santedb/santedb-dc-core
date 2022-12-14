using SanteDB.Client.Upstream.Management;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Services;
using SanteDB.Persistence.MDM.Model;
using SanteDB.Rest.AMI;
using SanteDB.Rest.Common;
using SanteDB.Rest.HDSI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

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
            typeof(UpstreamSecurityChallengeProvider),
            typeof(UpstreamAuditRepository),
            typeof(UpstreamJobManager),
            typeof(UpstreamPubSubManager)
        };
        private readonly IServiceManager m_serviceManager;
        private readonly IDictionary<Type, Type> m_wrappedAmiResources = new Dictionary<Type, Type>()
        {
            {  typeof(SecurityUser), typeof(SecurityUserInfo) },
            {  typeof(SecurityRole), typeof(SecurityRoleInfo) },
            {  typeof(SecurityDevice), typeof(SecurityDeviceInfo) },
            {  typeof(SecurityApplication), typeof(SecurityApplicationInfo) }
        };

        private Type[] m_amiResources = new Type[]
        {
            typeof(SubscriptionDefinition),
            typeof(SecurityProvenance),
            typeof(ApplicationEntity),
            typeof(DeviceEntity),
            typeof(SecurityPolicy),
            typeof(SecurityChallenge),
            typeof(MailMessage),
            typeof(IdentityDomain),
            typeof(IdentifierType),
            typeof(ExtensionType)
        };

        /// <summary>
        /// Constructor
        /// </summary>
        public UpstreamRepositoryFactory(IServiceManager serviceManager)
        {
            this.m_serviceManager = serviceManager;
            foreach (var t in typeof(Entity).Assembly.ExportedTypes.Where(o => typeof(Entity).IsAssignableFrom(o)))
                ModelSerializationBinder.RegisterModelType(typeof(EntityMaster<>).MakeGenericType(t));
            ModelSerializationBinder.RegisterModelType(typeof(EntityRelationshipMaster));
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
            

            var serviceCandidate = this.m_upstreamServices.FirstOrDefault(o => serviceType.IsAssignableFrom(o));
            if(serviceCandidate != null)
            {
                serviceInstance = this.m_serviceManager.CreateInjected(serviceCandidate);
                return true;
            }
            else if(serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IRepositoryService<>))
            {
                var storageType = serviceType.GenericTypeArguments[0];
                if(this.m_amiResources.Contains(storageType))
                {
                    storageType = typeof(AmiUpstreamRepository<>).MakeGenericType(storageType);
                }
                else if (this.m_wrappedAmiResources.TryGetValue(storageType, out var wrapperType))
                {
                    storageType = typeof(AmiWrappedUpstreamRepository<,>).MakeGenericType(storageType, wrapperType);
                }
                else if(storageType.GetCustomAttribute<XmlRootAttribute>() != null)
                {
                    storageType = typeof(HdsiUpstreamRepository<>).MakeGenericType(storageType);
                }
                else
                {
                    serviceInstance = null;
                    return false;
                }
                serviceInstance = this.m_serviceManager.CreateInjected(storageType);
                return true;
            }
            serviceInstance = null;
            return false;
        }


    }
}
