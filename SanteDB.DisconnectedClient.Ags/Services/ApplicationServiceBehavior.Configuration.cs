/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Http.Description;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Interop.AMI;
using SanteDB.DisconnectedClient.Interop.HDSI;
using SanteDB.DisconnectedClient.Mail;
using SanteDB.DisconnectedClient.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Services.Local;
using SanteDB.DisconnectedClient.Services.Remote;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.DisconnectedClient.Tickler;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Data;
using SanteDB.DisconnectedClient.Diagnostics;
using SanteDB.DisconnectedClient.Http;
using SanteDB.DisconnectedClient.Security;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Configuration;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Data;
using SanteDB.Core;
using SanteDB.BI.Services.Impl;
using SanteDB.Core.Model;
using SanteDB.DisconnectedClient.Security.Audit;
using SanteDB.Core.Auditing;
using SanteDB.DisconnectedClient.Ags.Configuration;
using SanteDB.Core.Jobs;
using SanteDB.Messaging.HDSI.Client;
using System.Net;
using SanteDB.Core.Interfaces;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Application Services Interface Behavior
    /// </summary>
    public partial class ApplicationServiceBehavior : IApplicationServiceContract
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(ApplicationServiceBehavior));

        /// <summary>
        /// Get the configuration
        /// </summary>
        public ConfigurationViewModel GetConfiguration()
        {
            return new ConfigurationViewModel(ApplicationContext.Current.Configuration);
        }

        /// <summary>
        /// Push configuration to a remote target
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public List<String> PushConfiguration(TargetedConfigurationViewModel model)
        {
            try
            {
                var svc = ApplicationServiceContext.Current.GetService<IConfigurationPushService>();
                if (svc == null) throw new InvalidOperationException("Cannot find configuration push service");
                return svc.Configure(new Uri(model.RemoteUri), model.UserName, model.Password, model.Parameters).Select(o => o.ToString()).ToList();
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Error sending configuration details to {0} - {1}", model.RemoteUri, e);
                throw new Exception($"Failed to push relevant configuration details to {model.RemoteUri}", e);
            }
        }

        /// <summary>
        /// Get the configuration for the specified user
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public ConfigurationViewModel GetUserConfiguration()
        {
            return new ConfigurationViewModel(ApplicationContext.Current.GetUserConfiguration(AuthenticationContext.Current.Principal.Identity.Name));
        }

        /// <summary>
        /// Join the realm
        /// </summary>
        public ConfigurationViewModel JoinRealm(JObject configData)
        {
            String realmUri = configData["realmUri"].Value<String>(),
                deviceName = configData["deviceName"].Value<String>(),
                domainSecurity = configData["domainSecurity"].Value<String>();
            Int32 port = configData["port"].Value<Int32>();
            Boolean enableSSL = configData["enableSSL"].Value<Boolean>(),
                enableTrace = configData["enableTrace"].Value<Boolean>(),
                replaceExisting = configData["replaceExisting"].Value<Boolean>();

            if (configData.ContainsKey("client_secret"))
                ApplicationContext.Current.Application.ApplicationSecret = configData["client_secret"].Value<String>();

            // Set domain security
            switch (domainSecurity)
            {
                case "Basic":
                    ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DomainAuthentication = DomainClientAuthentication.Basic;
                    break;
                case "Inline":
                    ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DomainAuthentication = DomainClientAuthentication.Inline;
                    break;
                case "Peer":
                    ApplicationContext.Current.RemoveServiceProvider(typeof(OAuthIdentityProvider));
                    break;
            }
            this.m_tracer.TraceInfo("Joining {0}", realmUri);

            // Stage 1 - Demand access admin policy
            try
            {

                new PolicyPermission(PermissionState.Unrestricted, PermissionPolicyIdentifiers.CreateDevice).Demand();
                new PolicyPermission(PermissionState.Unrestricted, PermissionPolicyIdentifiers.AccessClientAdministrativeFunction).Demand();
                new PolicyPermission(PermissionState.Unrestricted, PermissionPolicyIdentifiers.UnrestrictedMetadata).Demand();

                // We're allowed to access server admin!!!! Yay!!!
                // We're goin to conigure the realm settings now (all of them)
                var serviceClientSection = ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>();
                if (serviceClientSection == null)
                {
                    serviceClientSection = new ServiceClientConfigurationSection()
                    {
                        RestClientType = typeof(RestClient)
                    };
                    ApplicationContext.Current.Configuration.AddSection(serviceClientSection);
                }

                // TODO: Actually contact the AMI for this information
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceName = deviceName;
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = realmUri;
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().TokenAlgorithms = new System.Collections.Generic.List<string>() {
                    "RS256",
                    "HS256"
                };
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().TokenType = "urn:ietf:params:oauth:token-type:jwt";
                // Parse ACS URI
                var scheme = enableSSL ? "https" : "http";

                // AMI Client
                AmiServiceClient amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

                // We get the ami options for the other configuration
                var serviceOptions = amiClient.Options();
                serviceClientSection.Client.Clear();

                Dictionary<ServiceEndpointType, String> endpointNames = new Dictionary<ServiceEndpointType, string>()
                {
                    { ServiceEndpointType.AdministrationIntegrationService, "ami" },
                    { ServiceEndpointType.HealthDataService, "hdsi" },
                    { ServiceEndpointType.AuthenticationService, "acs" },
                    {ServiceEndpointType.BusinessIntelligenceService, "bis" }
                };

                foreach (var itm in serviceOptions.Endpoints)
                {

                    var urlInfo = itm.BaseUrl.Where(o => o.StartsWith(scheme));
                    String serviceName = null;
                    if (!urlInfo.Any() || !endpointNames.TryGetValue(itm.ServiceType, out serviceName))
                        continue;

                    // Description binding
                    ServiceClientDescription description = new ServiceClientDescription()
                    {
                        Binding = new ServiceClientBinding()
                        {
                            Security = new ServiceClientSecurity()
                            {
                                AuthRealm = realmUri,
                                Mode = itm.Capabilities.HasFlag(ServiceEndpointCapabilities.BearerAuth) ? SecurityScheme.Bearer :
                                    itm.Capabilities.HasFlag(ServiceEndpointCapabilities.BasicAuth) ? SecurityScheme.Basic :
                                    SecurityScheme.None,
                                CredentialProvider = itm.Capabilities.HasFlag(ServiceEndpointCapabilities.BearerAuth) ? (ICredentialProvider)new TokenCredentialProvider() :
                                    itm.Capabilities.HasFlag(ServiceEndpointCapabilities.BasicAuth) ?
                                    (ICredentialProvider)(itm.ServiceType == ServiceEndpointType.AuthenticationService ? (ICredentialProvider)new OAuth2CredentialProvider() : new HttpBasicTokenCredentialProvider()) :
                                    null,
                                PreemptiveAuthentication = itm.Capabilities != ServiceEndpointCapabilities.None
                            },
                            Optimize = itm.Capabilities.HasFlag(ServiceEndpointCapabilities.Compression),
                            OptimizationMethod = itm.Capabilities.HasFlag(ServiceEndpointCapabilities.Compression) ? OptimizationMethod.Gzip : OptimizationMethod.None,
                        },
                        Endpoint = urlInfo.Select(o => new ServiceClientEndpoint()
                        {
                            Address = o.Replace("0.0.0.0", realmUri),
                            Timeout = itm.ServiceType == ServiceEndpointType.HealthDataService ? 60000 : 30000
                        }).ToList(),
                        Trace = enableTrace,
                        Name = serviceName
                    };

                    serviceClientSection.Client.Add(description);
                }

                // Re-initialize the service providers
                ApplicationContext.Current.RemoveServiceProvider(typeof(AmiPolicyInformationService));
                ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteRepositoryService));
                ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteAssigningAuthorityService));
                ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteSecurityRepository));
                ApplicationContext.Current.AddServiceProvider(typeof(AmiPolicyInformationService));
                ApplicationContext.Current.AddServiceProvider(typeof(RemoteAssigningAuthorityService));
                ApplicationContext.Current.AddServiceProvider(typeof(RemoteRepositoryService));
                ApplicationContext.Current.AddServiceProvider(typeof(RemoteSecurityRepository));
                ApplicationContext.Current.GetService<RemoteRepositoryService>().Start();

                // Cache address types
                ApplicationContext.Current.GetService<IRepositoryService<Concept>>().Find(o => o.ConceptSets.Any(s => s.Key == ConceptSetKeys.AddressComponentType));
                EntitySource.Current = new EntitySource(new RepositoryEntitySource());

                byte[] pcharArray = Guid.NewGuid().ToByteArray();
                char[] spec = { '@', '#', '$', '*', '~' };
                for (int i = 0; i < pcharArray.Length; i++)
                    switch (i % 5)
                    {
                        case 0:
                            pcharArray[i] = (byte)((pcharArray[i] % 10) + 48);
                            break;
                        case 1:
                            pcharArray[i] = (byte)spec[pcharArray[i] % spec.Length];
                            break;
                        case 2:
                            pcharArray[i] = (byte)((pcharArray[i] % 25) + 65);
                            break;
                        case 3:
                            pcharArray[i] = (byte)((pcharArray[i] % 25) + 97);
                            break;
                        default:
                            pcharArray[i] = (byte)((pcharArray[i] % 61) + 65);
                            break;
                    }

                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret = Encoding.ASCII.GetString(pcharArray);
                                
                // Create the necessary device user
                try
                {
                    // Recreate the client with the updated security configuration
                    amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                    var hdsiClient = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));

                    // Create application user
                    var role = amiClient.GetRole("SYNCHRONIZERS");

                    var osiService = ApplicationServiceContext.Current.GetService<IOperatingSystemInfoService>();
                    // lookup existing device
                    var existingDevice = amiClient.GetDevices(o => o.Name == deviceName);
                    if (existingDevice.CollectionItem.Count == 0)
                    {
                        // Create device
                        var newDevice = amiClient.CreateDevice(new SecurityDeviceInfo(new SanteDB.Core.Model.Security.SecurityDevice()
                        {
                            CreationTime = DateTimeOffset.Now,
                            Name = deviceName,
                            DeviceSecret = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret
                        })
                        {
                            Policies = role.Policies
                        });

                        // Create the device entity 
                        hdsiClient.Create(new DeviceEntity()
                        {
                            SecurityDevice = newDevice.Entity,
                            StatusConceptKey = StatusKeys.Active,
                            ManufacturerModelName = osiService.ManufacturerName,
                            OperatingSystemName = osiService.VersionString,
                            GeoTag = ApplicationContext.Current.GetService<IGeoTaggingService>()?.GetCurrentPosition(),
                            Names = new List<EntityName>()
                                {
                                    new EntityName(NameUseKeys.Assigned, deviceName),
                                    new EntityName(NameUseKeys.Search, osiService.MachineName)
                                }
                        });
                    }
                    else
                    {
                        if (!configData.ContainsKey("replaceExisting") || !configData["replaceExisting"].Value<Boolean>())
                        {
                            ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret = ApplicationContext.Current.Device.DeviceSecret = null;
                            throw new DuplicateNameException(Strings.err_duplicate_deviceName);
                        }
                        else
                        {
                            var existingDeviceItem = existingDevice.CollectionItem.OfType<SecurityDeviceInfo>().First().Entity;
                            amiClient.UpdateDevice(existingDeviceItem.Key.Value, new SecurityDeviceInfo(new SanteDB.Core.Model.Security.SecurityDevice()
                            {

                                UpdatedTime = DateTime.Now,
                                Name = deviceName,
                                DeviceSecret = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret
                            })
                            {
                                Policies = role.Policies
                            });

                            // Create the device entity 
                            var existingDeviceEntity = hdsiClient.Query<DeviceEntity>(o => o.SecurityDeviceKey == existingDeviceItem.Key.Value, 0, 1, false).Item.OfType<DeviceEntity>();
                            foreach (var ede in existingDeviceEntity)
                                hdsiClient.Obsolete(ede);

                            hdsiClient.Create(new DeviceEntity()
                            {
                                SecurityDevice = existingDeviceItem,
                                StatusConceptKey = StatusKeys.Active,
                                ManufacturerModelName = osiService.ManufacturerName,
                                OperatingSystemName = osiService.VersionString,
                                GeoTag = ApplicationContext.Current.GetService<IGeoTaggingService>()?.GetCurrentPosition(),
                                Names = new List<EntityName>()
                                {
                                    new EntityName(NameUseKeys.Assigned, deviceName),
                                    new EntityName(NameUseKeys.Search, osiService.MachineName)
                                }
                            });

                        }
                    }
                }
                catch (DuplicateNameException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error registering device account: {0}", e);
                    throw new Exception($"Error registering device {deviceName} on realm {realmUri}", e);
                }
                return new ConfigurationViewModel(ApplicationContext.Current.Configuration);
            }
            catch (PolicyViolationException ex)
            {

                this.m_tracer.TraceWarning("Policy violation exception on {0}. Will attempt again", ex.Demanded, ex.ToString());
                // Only configure the minimum to contact the realm for authentication to continue
                var serviceClientSection = ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>();
                if (serviceClientSection == null)
                {
                    serviceClientSection = new ServiceClientConfigurationSection()
                    {
                        RestClientType = typeof(RestClient)
                    };
                    ApplicationContext.Current.Configuration.AddSection(serviceClientSection);
                }

                // TODO: Actually contact the AMI for this information
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceName = deviceName;
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = realmUri;
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().TokenAlgorithms = new System.Collections.Generic.List<string>() {
                    "RS256",
                    "HS256"
                };

                // AMI Client
                serviceClientSection.Client.Clear();

                var scheme = enableSSL ? "https" : "http";
                string amiUri = String.Format("{0}://{1}:{2}/ami", scheme,
                    realmUri,
                    port),
                    hdsiUri = String.Format("{0}://{1}:{2}/hdsi", scheme,
                    realmUri,
                    port);

                serviceClientSection.Client.Add(new ServiceClientDescription()
                {
                    Binding = new ServiceClientBinding()
                    {
                        Optimize = false
                    },
                    Endpoint = new System.Collections.Generic.List<ServiceClientEndpoint>() {
                        new ServiceClientEndpoint() {
                            Address = amiUri, Timeout = 30000
                        }
                    },
                    Name = "ami",
                    Trace = enableTrace
                });
                serviceClientSection.Client.Add(new ServiceClientDescription()
                {
                    Binding = new ServiceClientBinding()
                    {
                        Optimize = true
                    },
                    Endpoint = new System.Collections.Generic.List<ServiceClientEndpoint>() {
                        new ServiceClientEndpoint() {
                            Address = hdsiUri, Timeout = 30000
                        }
                    },
                    Name = "hdsi",
                    Trace = enableTrace
                });

                ApplicationContext.Current.AddServiceProvider(typeof(AmiPolicyInformationService));
                ApplicationContext.Current.AddServiceProvider(typeof(RemoteRepositoryService));
                ApplicationContext.Current.AddServiceProvider(typeof(RemoteSecurityRepository));

                AmiServiceClient amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

                // We get the ami options for the other configuration
                try
                {
                    var serviceOptions = amiClient.Options();

                    var acsOption = serviceOptions.Endpoints.FirstOrDefault(o => o.ServiceType == ServiceEndpointType.AuthenticationService);

                    if (acsOption == null)
                    {
                        ApplicationContext.Current.RemoveServiceProvider(typeof(OAuthIdentityProvider), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(OAuthDeviceIdentityProvider), true);
                        //ApplicationContext.Current.AddServiceProvider(typeof(HttpBasicIdentityProvider), true);
                        //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(HttpBasicIdentityProvider).AssemblyQualifiedName);
                    }
                    else
                    {
                        // ApplicationContext.Current.RemoveServiceProvider(typeof(HttpBasicIdentityProvider), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(OAuthDeviceIdentityProvider), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(OAuthIdentityProvider), true);

                        // Parse ACS URI
                        serviceClientSection.Client.Add(new ServiceClientDescription()
                        {
                            Binding = new ServiceClientBinding()
                            {
                                Security = new ServiceClientSecurity()
                                {
                                    AuthRealm = realmUri,
                                    Mode = SecurityScheme.Basic,
                                    CredentialProvider = new OAuth2CredentialProvider()
                                },
                                Optimize = false
                            },
                            Name = "acs",
                            Trace = enableTrace,
                            Endpoint = acsOption.BaseUrl.Select(o => new ServiceClientEndpoint()
                            {
                                Address = o.Replace("0.0.0.0", realmUri),
                                Timeout = 30000
                            }).ToList()
                        });

                    }

                    // Update the security binding on the temporary AMI binding
                    acsOption = serviceOptions.Endpoints.FirstOrDefault(o => o.ServiceType == ServiceEndpointType.AdministrationIntegrationService);

                    serviceClientSection.Client[0].Binding.Security = new ServiceClientSecurity()
                    {
                        AuthRealm = realmUri,
                        Mode = acsOption.Capabilities.HasFlag(ServiceEndpointCapabilities.BearerAuth) ? SecurityScheme.Bearer :
                                    acsOption.Capabilities.HasFlag(ServiceEndpointCapabilities.BasicAuth) ? SecurityScheme.Basic :
                                    SecurityScheme.None,
                        CredentialProvider = acsOption.Capabilities.HasFlag(ServiceEndpointCapabilities.BearerAuth) ? (ICredentialProvider)new TokenCredentialProvider() :
                                    acsOption.Capabilities.HasFlag(ServiceEndpointCapabilities.BasicAuth) ?
                                    (ICredentialProvider)(acsOption.ServiceType == ServiceEndpointType.AuthenticationService ? (ICredentialProvider)new OAuth2CredentialProvider() : new HttpBasicTokenCredentialProvider()) :
                                    null,
                        PreemptiveAuthentication = acsOption.Capabilities != ServiceEndpointCapabilities.None
                    };

                    throw new PolicyViolationException(AuthenticationContext.Current.Principal, ex.PolicyId, SanteDB.Core.Model.Security.PolicyGrantType.Deny);

                }
                finally
                {
                    ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = null;
                }
            }
            catch (DuplicateNameException) // handles duplicate device name
            {

                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = null;
                throw;
            }
            catch (WebException e) when (e.Message.StartsWith("The remote name could not be resolved"))
            {
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = null;
                this.m_tracer.TraceError("Error joining context: {0}", e);
                throw new Exception($"Error Joining Domain - {e.Message}", e);
            }
            catch (WebException e) 
            {
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = null;
                this.m_tracer.TraceError("Error joining context: {0}", e);
                throw new Exception($"Remote server returned error - {e.Message}", e);
            }
            catch (Exception e)
            {
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = null;
                this.m_tracer.TraceError("Error joining context: {0}", e);
                throw new Exception($"Could not complete joining context", e);
            }
        }

        /// <summary>
        /// Save the user configuration
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public void SaveUserConfiguration(ConfigurationViewModel model)
        {
            ApplicationContext.Current.SaveUserConfiguration(AuthenticationContext.Current.Principal.Identity.Name,
                new SanteDBConfiguration()
                {
                    Sections = new List<object>()
                    {
                        model.Application,
                        model.Applet
                    }
                }
            );
        }

        /// <summary>
        /// Remove the specified service from the DCG
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public void DisableService(String serviceIdentifier)
        {
            try
            {
                var svc = ApplicationServiceContext.Current.GetService<IServiceManager>().GetServices().FirstOrDefault(o => o.GetType().FullName.Equals(serviceIdentifier, StringComparison.OrdinalIgnoreCase));
                var serviceType = svc?.GetType() ?? Type.GetType(serviceIdentifier);
                if (serviceType == null)
                    throw new KeyNotFoundException($"Service {serviceIdentifier} not found");

                (ApplicationServiceContext.Current.GetService(serviceType) as IDaemonService)?.Stop();
                ApplicationContext.Current.RemoveServiceProvider(serviceType, true);
                ApplicationContext.Current.ConfigurationPersister.Save(ApplicationContext.Current.Configuration);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error disabling service : {0}", e);
                throw new Exception($"Could not disable service {serviceIdentifier}", e);
            }
        }

        /// <summary>
        /// Remove the specified service from the DCG
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public void EnableService(String serviceIdentifier)
        {
            try
            {
                var svc = ApplicationServiceContext.Current.GetService<IServiceManager>().GetAllTypes().FirstOrDefault(o => o.FullName.Equals(serviceIdentifier, StringComparison.OrdinalIgnoreCase));
                var serviceType = svc?.GetType() ?? Type.GetType(serviceIdentifier);
                if (serviceType == null)
                    throw new KeyNotFoundException($"Service {serviceIdentifier} not found");

                ApplicationContext.Current.AddServiceProvider(serviceType, true);
                (ApplicationServiceContext.Current.GetService(serviceType) as IDaemonService)?.Start();
                ApplicationContext.Current.ConfigurationPersister.Save(ApplicationContext.Current.Configuration);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error enabling service: {0}", e);
                throw new Exception($"Could not enable service {serviceIdentifier}", e);
            }
        }

        /// <summary>
        /// Update the configuration of the service
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public ConfigurationViewModel UpdateConfiguration(ConfigurationViewModel configuration)
        {
            // We will be rewriting the configuration
            if (!(ApplicationContext.Current as ApplicationContext).ConfigurationPersister.IsConfigured)
                ApplicationContext.Current.Configuration.RemoveSection<SynchronizationConfigurationSection>();

            // Did the user add any other service definitions?
            foreach (var svc in configuration.Application.ServiceProviders.Where(st => !ApplicationContext.Current.Configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Any(ct => ct.Type == st.Type) && ApplicationContext.Current.GetService(st.Type) == null))
                ApplicationContext.Current.AddServiceProvider(svc.Type, true);

            ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().AppletSolution = configuration.Applet.AppletSolution;
            ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().AutoUpdateApplets = configuration.Applet.AutoUpdateApplets;
            // Data mode
            switch (configuration.Synchronization.Mode)
            {
                case SynchronizationMode.Online:
                    {
                        // Remove all data persistence services
                        foreach (var idp in ApplicationContext.Current.GetServices().Where(o => o is IDataPersistenceService
                                || o is IPolicyInformationService
                                || o is ISecurityRepositoryService
                                || o is IAuditRepositoryService
                                || o is IJobManagerService
                                || o is ISynchronizationService
                                || o is IMailMessageRepositoryService).ToArray())
                            ApplicationContext.Current.RemoveServiceProvider(idp.GetType());
                        ApplicationContext.Current.AddServiceProvider(typeof(AmiPolicyInformationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteRepositoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteAssigningAuthorityService), true);

                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteSecurityRepository), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteAuditRepositoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteMailRepositoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteBiService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteJobManager), true);
                        break;
                    }
                case SynchronizationMode.Offline:
                    {
                        throw new NotSupportedException();
                    }
                case SynchronizationMode.Sync:
                    {

                        // First, we want to update the central repository to let it know information about us
                        if (configuration.Security.Facilities?.Count > 0 || configuration.Security.Owners?.Count > 0)
                        {
                            var deviceEntity = ApplicationServiceContext.Current.GetService<IRepositoryService<DeviceEntity>>().Find(o => o.SecurityDevice.Name == ApplicationContext.Current.Device.Name, 0, 1, out int t).FirstOrDefault();
                            if(deviceEntity != null)
                            {
                                deviceEntity.Relationships.RemoveAll(r => r.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation || r.RelationshipTypeKey == EntityRelationshipTypeKeys.AssignedEntity);
                                deviceEntity.Relationships.AddRange(configuration.Security.Facilities?.Select(o => new EntityRelationship(EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation, o)) ?? new EntityRelationship[0]);
                                deviceEntity.Relationships.AddRange(configuration.Security.Owners?.Select(o => new EntityRelationship(EntityRelationshipTypeKeys.AssignedEntity, o)) ?? new EntityRelationship[0]);
                                ApplicationServiceContext.Current.GetService<IRepositoryService<DeviceEntity>>().Save(deviceEntity);
                            }
                        }

                        this.m_tracer.TraceInfo("Removing remote service providers....");
                        // Remove any references to remote storage providers
                        ApplicationContext.Current.RemoveServiceProvider(typeof(AmiPolicyInformationService), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteRepositoryService), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteSecurityRepository), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteAuditRepositoryService), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteBiService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(SynchronizedAuditDispatchService), true);
                        // Configure the selected storage provider
                        this.m_tracer.TraceInfo("Configuration of data service provider....");

                        var storageProvider = StorageProviderUtil.GetProvider(configuration.Data.Provider);
                        configuration.Data.Options.Add("DataDirectory", ApplicationContext.Current.ConfigurationPersister.ApplicationDataDirectory);
                        storageProvider.Configure(ApplicationContext.Current.Configuration, configuration.Data.Options);

                        // Remove all data persistence services
                        foreach (var idp in ApplicationContext.Current.GetServices().Where(o => o is IDataPersistenceService
                                || o is IPolicyInformationService
                                || o is ISecurityRepositoryService
                                || o is IJobManagerService
                                || o is IAuditRepositoryService
                                || o is ISynchronizationService
                                || o is IMailMessageRepositoryService).ToArray())
                            ApplicationContext.Current.RemoveServiceProvider(idp.GetType());

                        this.m_tracer.TraceInfo("Adding local service provider....");

                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteSynchronizationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalMailService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(HdsiIntegrationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(AmiIntegrationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(MailSynchronizationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalJobManagerService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalRepositoryFactoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalRepositoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalSecurityRepository), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalTagPersistenceService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(PersistenceEntitySource), true);
                        //ApplicationContext.Current.AddServiceProvider(typeof(LocalCarePlanManagerService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(SystemPolicySynchronizationDaemon), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(SynchronizedAuditDispatchService), true);
                        // BI Services
                        ApplicationContext.Current.AddServiceProvider(typeof(AppletBiRepository), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(InMemoryPivotProvider), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalBiRenderService), true);

                        // TODO: Register execution engine
                        // Sync settings
                        var syncConfig = new SynchronizationConfigurationSection()
                        {
                            ForbiddenResouces = new List<SynchronizationForbidConfiguration>()
                            {
                                new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "DeviceEntity"),
                                new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "ApplicationEntity"),
                                new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "Concept"),
                                new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "ConceptSet"),
                                new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "Place"),
                                new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "ReferenceTerm"),
                                new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "AssigningAuthority"),
                                new SynchronizationForbidConfiguration(SynchronizationOperationType.Obsolete, "UserEntity")
                            }
                        };

                        // Since we're running offline we don't want to audit certain events
                        configuration.OtherSections.OfType<AuditAccountabilityConfigurationSection>().First().AuditFilters.AddRange(new AuditFilterConfiguration[]
                            {
                                // Never audit security alerts which are successful 
                                new AuditFilterConfiguration(ActionType.Execute, EventIdentifierType.SecurityAlert | EventIdentifierType.UseOfRestrictedFunction | EventIdentifierType.NetworkActivity, OutcomeIndicator.Success, false, false),
                            });

                        var binder = new SanteDB.Core.Model.Serialization.ModelSerializationBinder();

                        this.m_tracer.TraceInfo("Configuring Subscription....");

                        var subscribeToIdentifiers = configuration.Synchronization.SubscribeTo;
                        foreach (var id in subscribeToIdentifiers)
                        {
                            IdentifiedData itm = null;
                            switch (configuration.Synchronization.SubscribeType)
                            {
                                case "AssigningAuthority":
                                    itm = ApplicationContext.Current.GetService<IRepositoryService<AssigningAuthority>>().Get(Guid.Parse(id.ToString()), Guid.Empty);
                                    break;
                                case "Place":
                                case "Facility":
                                    itm = ApplicationContext.Current.GetService<IRepositoryService<Place>>().Get(Guid.Parse(id.ToString()), Guid.Empty);

                                    // Load guards on comment objects
                                    (itm as Place).Addresses.ForEach(o =>
                                    {
                                        o.AddressUse = o.AddressUse ?? ApplicationServiceContext.Current.GetService<IRepositoryService<Concept>>().Get(o.AddressUseKey.GetValueOrDefault());
                                        o.Component.ForEach(c => c.ComponentType = c.ComponentType ?? ApplicationServiceContext.Current.GetService<IRepositoryService<Concept>>().Get(c.ComponentTypeKey.GetValueOrDefault()));
                                    });
                                    break;

                            }

                            // Subscription data
                            // TODO: Check if mode is ALL
                            var subscriptions = configuration.Synchronization.SynchronizationResources;

                            syncConfig.SynchronizationResources.AddRange(subscriptions.Select(sub => new SynchronizationResource()
                            {
                                Name = sub.Name,
                                ResourceAqn = sub.ResourceAqn,
                                Triggers = sub.Triggers,
                                Always = sub.Always,
                                Filters = sub.Filters.Select(f =>
                                {
                                    var filter = f.ToString();

                                    this.m_tracer.TraceInfo("Configure subscription for {0}", filter);
                                    while (filter.Contains("$"))
                                    {
                                        int spos = filter.IndexOf("$") + 1,
                                            slen = filter.IndexOf("$", spos) - spos;

                                        var varName = filter.Substring(spos, slen);
                                        var pexpr = QueryExpressionParser.BuildPropertySelector(itm.GetType(), varName.Substring(varName.IndexOf(".") + 1));

                                        // Evaluate
                                        var sval = pexpr.Compile().DynamicInvoke(itm);
                                        filter = filter.Replace($"${varName}$", sval?.ToString());
                                    }

                                    return filter;
                                }).ToList()
                            }));
                        }

                        ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteAssigningAuthorityService), true);

                        // Synchronization resources
                        syncConfig.SynchronizationResources.Add(new SynchronizationResource()
                        {
                            ResourceAqn = "EntityRelationship",
                            Triggers = SynchronizationPullTriggerType.OnCommit
                        });

                        this.m_tracer.TraceInfo("Cleaning up subscriptions...");

                        syncConfig.SubscribeTo = configuration.Synchronization.SubscribeTo;
                        syncConfig.SynchronizationResources = syncConfig.SynchronizationResources.GroupBy(o => o.Name + o.Triggers.ToString())
                            .Select(g =>
                            {
                                var retVal = g.First();
                                retVal.Filters = g.SelectMany(r => r.Filters).Distinct().ToList();
                                return retVal;
                            })
                            .ToList();

                        syncConfig.Mode = SynchronizationMode.Sync;
                        if (configuration.Synchronization.PollIntervalXml != "00:00:00")
                            syncConfig.PollIntervalXml = configuration.Synchronization.PollIntervalXml;
                        ApplicationContext.Current.Configuration.AddSection(syncConfig);

                        break;
                    }
            }


            // Password hashing
            this.m_tracer.TraceInfo("Setting password hasher...");

            var pwh = ApplicationContext.Current.GetService<IPasswordHashingService>();
            if (pwh != null)
                ApplicationContext.Current.RemoveServiceProvider(pwh.GetType());

            switch (configuration.Security.Hasher)
            {
                case "SHA256PasswordHasher":
                    ApplicationContext.Current.AddServiceProvider(typeof(SHA256PasswordHasher), true);
                    break;
                case "SHAPasswordHasher":
                    ApplicationContext.Current.AddServiceProvider(typeof(SHAPasswordHasher), true);
                    break;
                case "PlainTextPasswordHasher":
                    ApplicationContext.Current.AddServiceProvider(typeof(PlainTextPasswordHasher), true);
                    break;
            }

            this.m_tracer.TraceInfo("Setting application secret...");

            // Repository service provider
            ApplicationContext.Current.AddServiceProvider(typeof(RepositoryEntitySource), true);
            // Override application secret
            ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().ApplicationSecret = configuration.Security.ApplicationSecret;

            // Audit retention.
            ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().AuditRetention = TimeSpan.Parse(configuration.Security.AuditRetentionXml);

            if (configuration.Security.RestrictLoginToFacilityUsers)
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().RestrictLoginToFacilityUsers = true;
            if(configuration.Security.Facilities?.Count > 0)
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Facilities= configuration.Security.Facilities;
            if (configuration.Security.Owners?.Count > 0)
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Owners = configuration.Security.Owners;

            // Proxy
            if (!String.IsNullOrEmpty(configuration.Network.ProxyAddress))
                ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>().ProxyAddress = configuration.Network.ProxyAddress;

            var optimize = configuration.Network.Optimize;

            foreach (var itm in ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>().Client)
                if (itm.Binding.Optimize)
                    itm.Binding.OptimizationMethod = optimize;

            // Log settings
            var logSettings = ApplicationContext.Current.Configuration.GetSection<DiagnosticsConfigurationSection>();
            logSettings.TraceWriter = new System.Collections.Generic.List<TraceWriterConfiguration>()
            {
#if DEBUG
                new TraceWriterConfiguration () {
                        Filter = System.Diagnostics.Tracing.EventLevel.Critical,
                        InitializationData = "SanteDB",
                        TraceWriter = typeof(LogTraceWriter)
                    },
#endif
                new TraceWriterConfiguration()
                {
                    Filter = configuration.Log.Mode,
                    InitializationData = "SanteDB",
                    TraceWriter = typeof(FileTraceWriter)
                }
            };

            this.m_tracer.TraceInfo("Setting additional application settings...");

            if(configuration.Application.AppSettings != null)
                foreach (var i in configuration.Application.AppSettings)
                    ApplicationContext.Current.ConfigurationManager.SetAppSetting(i.Key, i.Value);

            // Other sections
            this.m_tracer.TraceInfo("Setting other settings...");
            if(configuration.OtherSections != null)
                foreach (var oth in configuration.OtherSections)
                {
                    if (ApplicationContext.Current.ConfigurationManager.Configuration.GetSection(oth.GetType()) != null)
                        ApplicationContext.Current.ConfigurationManager.Configuration.Sections.RemoveAll(o => o.GetType() == oth.GetType());
                    ApplicationContext.Current.ConfigurationManager.Configuration.AddSection(oth);
                }
            this.m_tracer.TraceInfo("Saving configuration options {0}", JsonConvert.SerializeObject(configuration));

            // Re-binding of AGS endpoints?
            var overrideBinding = configuration.Application.AppSettings.Find(o => o.Key == "http.bindAddress");
            if(ApplicationServiceContext.Current.HostType == SanteDBHostType.Gateway && !String.IsNullOrEmpty(overrideBinding?.Value))
            {
                var currentAgs = ApplicationContext.Current.Configuration.GetSection<AgsConfigurationSection>();
                foreach (var svc in currentAgs.Services)
                    foreach(var ep in svc.Endpoints)
                    {
                        var caddr = new Uri(ep.Address);
                        ep.Address = new Uri($"{caddr.Scheme}://{overrideBinding.Value}{caddr.AbsolutePath}").ToString() ;
                    }

            }
            ApplicationContext.Current.ConfigurationPersister.Save(ApplicationContext.Current.Configuration);

            return new ConfigurationViewModel(ApplicationContext.Current.Configuration);
        }
    }
}
