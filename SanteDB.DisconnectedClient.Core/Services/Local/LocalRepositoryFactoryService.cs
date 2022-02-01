/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.Services.Local
{
    /// <summary>
    /// Represents a generic resource repository factory
    /// </summary>
    public class LocalRepositoryFactoryService : IServiceFactory
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Local Data Repository Service Factory";

        // Trace source
        private Tracer m_tracer = Tracer.GetTracer(typeof(LocalRepositoryFactoryService));

        // Service manager
        private IServiceManager m_serviceManager;

        /// <summary>
        /// Create a DI injected service
        /// </summary>
        public LocalRepositoryFactoryService(IServiceManager serviceManager)
        {
            this.m_serviceManager = serviceManager;
        }

        // Add repository services
        private readonly Type[] r_repositoryServices = {
                typeof(LocalConceptRepository),
                typeof(GenericLocalMetadataRepository<IdentifierType>),
                typeof(GenericLocalConceptRepository<ReferenceTerm>),
                typeof(GenericLocalConceptRepository<CodeSystem>),
                typeof(GenericLocalConceptRepository<ConceptSet>),
                typeof(GenericLocalMetadataRepository<AssigningAuthority>),
                typeof(GenericLocalMetadataRepository<ExtensionType>),
                typeof(GenericLocalMetadataRepository<TemplateDefinition>),
                typeof(LocalMaterialRepository),
                typeof(LocalManufacturedMaterialRepository),
                typeof(LocalBatchRepository),
                typeof(LocalOrganizationRepository),
                typeof(LocalPatientRepository),
                typeof(LocalPlaceRepository),
                typeof(LocalEntityRelationshipRepository),
                typeof(LocalExtensionTypeRepository),
                typeof(LocalSecurityApplicationRepository),
                typeof(LocalSecurityDeviceRepository),
                typeof(LocalSecurityPolicyRepository),
                typeof(LocalSecurityRoleRepository),
                typeof(LocalSecurityUserRepository),
                typeof(LocalAssigningAuthorityRepository),
                typeof(LocalUserEntityRepository),
                typeof(GenericLocalMetadataRepository<DeviceEntity>),
                typeof(GenericLocalMetadataRepository<ApplicationEntity>),
                typeof(LocalSecurityRepository)
    };

        /// <summary>
        /// Try to create <typeparamref name="TService"/>
        /// </summary>
        public bool TryCreateService<TService>(out TService serviceInstance)
        {
            if (this.TryCreateService(typeof(TService), out object service))
            {
                serviceInstance = (TService)service;
                return true;
            }
            serviceInstance = default(TService);
            return false;
        }

        /// <summary>
        /// Attempt to create the specified service
        /// </summary>
        public bool TryCreateService(Type serviceType, out object serviceInstance)
        {
            // Is this service type in the services?
            var st = r_repositoryServices.FirstOrDefault(s => s == serviceType || serviceType.IsAssignableFrom(s));
            if (st == null && (typeof(IRepositoryService).IsAssignableFrom(serviceType) || serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IRepositoryService<>)))
            {
                if (serviceType.IsGenericType)
                {
                    var wrappedType = serviceType.GenericTypeArguments[0];
                    if (typeof(Act).IsAssignableFrom(wrappedType))
                    {
                        this.m_tracer.TraceInfo("Adding Act repository service for {0}...", wrappedType.Name);
                        st = typeof(GenericLocalActRepository<>).MakeGenericType(wrappedType);
                    }
                    else if (typeof(Entity).IsAssignableFrom(wrappedType))
                    {
                        this.m_tracer.TraceInfo("Adding Entity repository service for {0}...", wrappedType);
                        st = typeof(GenericLocalClinicalDataRepository<>).MakeGenericType(wrappedType);
                    }
                    else
                    {
                        this.m_tracer.TraceInfo("Adding generic repository service for {0}...", wrappedType);
                        st = typeof(GenericLocalRepository<>).MakeGenericType(wrappedType);
                    }
                }
                else
                {
                    st = serviceType;
                }
            }
            else if (st == null)
            {
                serviceInstance = null;
                return false;
            }

            serviceInstance = this.m_serviceManager.CreateInjected(st);
            return true;
        }
    }
}