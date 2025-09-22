﻿/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using DocumentFormat.OpenXml.Office2019.Drawing.Diagram11;
using SanteDB.Client.Upstream.Management;
using SanteDB.Client.Upstream.Security;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Services;
using SanteDB.Persistence.MDM.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
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
            typeof(UpstreamTfaService),
            typeof(UpstreamJobManager),
            typeof(UpstreamPubSubManager),
            typeof(UpstreamCertificateAssociationManager),
            typeof(UpstreamDataTemplateManagementService),
            typeof(UpstreamBiMetadataRepository)
        };

        private readonly IServiceManager m_serviceManager;
        private readonly IDictionary<Type, Type> m_wrappedAmiResources = new Dictionary<Type, Type>()
        {
            {  typeof(SecurityUser), typeof(SecurityUserInfo) },
            {  typeof(SecurityRole), typeof(SecurityRoleInfo) },
            {  typeof(SecurityDevice), typeof(SecurityDeviceInfo) },
            {  typeof(SecurityApplication), typeof(SecurityApplicationInfo) }
        };
        private readonly IUpstreamManagementService m_upstreamManager;
        private Type[] m_amiResources = new Type[]
        {
            typeof(SubscriptionDefinition),
            typeof(SecurityProvenance),
            typeof(ApplicationEntity),
            typeof(DeviceEntity),
            typeof(SecurityPolicy),
            typeof(SecurityChallenge),
            typeof(MailMessage),
            typeof(IdentityDomain)
        };

        /// <inheritdoc/>
        public string ServiceName => "Upstream Repository Factory";

        /// <summary>
        /// Constructor
        /// </summary>
        public UpstreamRepositoryFactory(IServiceManager serviceManager, IUpstreamManagementService upstreamManager)
        {
            this.m_serviceManager = serviceManager;
            foreach (var t in typeof(Entity).Assembly.GetExportedTypesSafe().Where(o => typeof(Entity).IsAssignableFrom(o)))
            {
                ModelSerializationBinder.RegisterModelType(typeof(EntityMaster<>).MakeGenericType(t));
            }

            this.m_upstreamManager = upstreamManager;
            ModelSerializationBinder.RegisterModelType(typeof(EntityRelationshipMaster));
        }

        /// <inheritdoc/>
        public bool TryCreateService<TService>(out TService serviceInstance)
        {
            if (this.TryCreateService(typeof(TService), out var inner))
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
            if (serviceCandidate != null)
            {
                serviceInstance = this.m_serviceManager.CreateInjected(serviceCandidate);
                return true;
            }
            else if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IRepositoryService<>))
            {
                var storageType = serviceType.GenericTypeArguments[0];
                // Is the upstream configured? If so we use the upstream data gathered from OPTIONS
                if (this.m_upstreamManager.IsConfigured())
                {
                    switch (UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint(storageType))
                    {
                        case Core.Interop.ServiceEndpointType.AdministrationIntegrationService:
                            if (this.m_wrappedAmiResources.TryGetValue(storageType, out var wrapperType))
                            {
                                storageType = typeof(AmiWrappedUpstreamRepository<,>).MakeGenericType(storageType, wrapperType);
                            }
                            else
                            {
                                storageType = typeof(AmiUpstreamRepository<>).MakeGenericType(storageType);
                            }
                            break;
                        case Core.Interop.ServiceEndpointType.HealthDataService:
                            storageType = typeof(HdsiUpstreamRepository<>).MakeGenericType(storageType);
                            break;
                        default:
                            serviceInstance = null;
                            return false;
                    }
                }
                else
                {
                    if (this.m_amiResources.Contains(storageType))
                    {
                        storageType = typeof(AmiUpstreamRepository<>).MakeGenericType(storageType);
                    }
                    else if (this.m_wrappedAmiResources.TryGetValue(storageType, out var wrapperType))
                    {
                        storageType = typeof(AmiWrappedUpstreamRepository<,>).MakeGenericType(storageType, wrapperType);
                    }
                    else if (storageType.GetCustomAttribute<XmlRootAttribute>() != null)
                    {
                        storageType = typeof(HdsiUpstreamRepository<>).MakeGenericType(storageType);
                    }
                }
                serviceInstance = this.m_serviceManager.CreateInjected(storageType);
                return true;
            }
            serviceInstance = null;
            return false;
        }


    }
}
