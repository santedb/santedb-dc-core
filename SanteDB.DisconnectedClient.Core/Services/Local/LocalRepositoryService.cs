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
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using System;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Services.Local
{
    /// <summary>
    /// Daemon service which adds all the repositories for acts
    /// </summary>
    public class LocalRepositoryService : IDaemonService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Local Repository Registration Service";

        // Trace source
        private Tracer m_tracer = Tracer.GetTracer(typeof(LocalRepositoryService));

        /// <summary>
        /// Return true if the act repository service is running
        /// </summary>
        public bool IsRunning => false;

        /// <summary>
        /// Fired when starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Fired when started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Start the service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            // Add repository services
            Type[] repositoryServices = {
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

            foreach (var t in repositoryServices)
            {
                this.m_tracer.TraceInfo("Adding repository service for {0}...", t);
                ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(t);
            }

            ApplicationServiceContext.Current.Started += (o, e) =>
            {
                foreach (var t in typeof(Patient).GetTypeInfo().Assembly.ExportedTypes
                                    .Where(t =>
                                        typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()) &&
                                        !t.GetTypeInfo().IsAbstract &&
                                        t.GetTypeInfo().GetCustomAttribute<XmlRootAttribute>() != null
                                    ))
                {
                    var irst = typeof(IRepositoryService<>).MakeGenericType(t);
                    var irsi = ApplicationContext.Current.GetService(irst);
                    if (irsi == null)
                    {
                        if (typeof(Act).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()))
                        {
                            this.m_tracer.TraceInfo("Adding Act repository service for {0}...", t.Name);
                            var mrst = typeof(GenericLocalActRepository<>).MakeGenericType(t);
                            ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(mrst);
                        }
                        else if (typeof(Entity).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()))
                        {
                            this.m_tracer.TraceInfo("Adding Entity repository service for {0}...", t.Name);
                            var mrst = typeof(GenericLocalClinicalDataRepository<>).MakeGenericType(t);
                            ApplicationServiceContext.Current.GetService<IServiceManager>().AddServiceProvider(mrst);
                        }
                    }
                }

                this.Started?.Invoke(this, EventArgs.Empty);

            };

            return true;
        }

        /// <summary>
        /// Stop the daemon service
        /// </summary>
        /// <returns></returns>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }

}