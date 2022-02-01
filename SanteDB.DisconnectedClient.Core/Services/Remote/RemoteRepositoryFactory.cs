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
 * Date: 2021-10-29
 */
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Security.Remote;
using SanteDB.Persistence.MDM.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Represents a persistence service which uses the HDSI only in online mode
    /// </summary>
    public class RemoteRepositoryFactory : IServiceFactory
    {
        /// <summary>
        /// Remote repository service types - pre made
        /// </summary>
        private readonly Type[] r_repositoryServices = new Type[] {
                typeof(RemoteAssigningAuthorityService),
                typeof(RemoteAuditRepositoryService),
                typeof(RemoteBiService),
                typeof(RemoteJobManager),
                typeof(RemoteMailRepositoryService),
                typeof(RemotePubSubManager),
                typeof(RemoteRecordMatchConfigurationService),
                typeof(RemoteSecurityRepository),
                typeof(RemoteResourceCheckoutService),
                typeof(RemoteSecurityChallengeRepositoryService)
            };

        /// <summary>
        /// Template keys
        /// </summary>
        private static ConcurrentDictionary<String, Guid> s_templateKeys = new ConcurrentDictionary<string, Guid>();

        // Configuration
        private readonly ApplicationServiceContextConfigurationSection m_configuration;

        // Localization service
        private readonly ILocalizationService m_localizationService;

        // Service manager
        private readonly IServiceManager m_serviceManager;

        /// <summary>
        /// Get all types from core classes of entity and act and create shims in the model serialization binder
        /// </summary>
        public RemoteRepositoryFactory(IConfigurationManager configurationManager, IServiceManager serviceManager, ILocalizationService localizationService)
        {
            foreach (var t in typeof(Entity).Assembly.ExportedTypes.Where(o => typeof(Entity).IsAssignableFrom(o)))
                ModelSerializationBinder.RegisterModelType(typeof(EntityMaster<>).MakeGenericType(t));
            foreach (var t in typeof(Act).Assembly.ExportedTypes.Where(o => typeof(Act).IsAssignableFrom(o)))
                ModelSerializationBinder.RegisterModelType(typeof(ActMaster<>).MakeGenericType(t));
            ModelSerializationBinder.RegisterModelType(typeof(EntityRelationshipMaster));

            this.m_localizationService = localizationService;
            this.m_serviceManager = serviceManager;
            this.m_configuration = configurationManager.GetSection<ApplicationServiceContextConfigurationSection>();
        }

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Remote Data Repository Factory";

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteRepositoryFactory));

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

                    if (wrappedType.GetCustomAttribute<XmlRootAttribute>() != null)
                    {
                        this.m_tracer.TraceInfo("Adding repository service for {0}...", wrappedType.Name);
                        st = typeof(RemoteRepositoryService<>).MakeGenericType(wrappedType);
                    }
                    else
                    {
                        serviceInstance = null;
                        return false;
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
    }
}