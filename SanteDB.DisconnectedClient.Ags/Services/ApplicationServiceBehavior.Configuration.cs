/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
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
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Data;
using SanteDB.DisconnectedClient.Core.Data.Warehouse;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Interop.AMI;
using SanteDB.DisconnectedClient.Core.Interop.HDSI;
using SanteDB.DisconnectedClient.Core.Mail;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Security.Remote;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Core.Services.Local;
using SanteDB.DisconnectedClient.Core.Services.Remote;
using SanteDB.DisconnectedClient.Core.Synchronization;
using SanteDB.DisconnectedClient.Core.Tickler;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Xamarin;
using SanteDB.DisconnectedClient.Xamarin.Data;
using SanteDB.DisconnectedClient.Xamarin.Diagnostics;
using SanteDB.DisconnectedClient.Xamarin.Http;
using SanteDB.DisconnectedClient.Xamarin.Security;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Messaging.HDSI.Client;
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

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// The application services behavior
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
            return new ConfigurationViewModel(XamarinApplicationContext.Current.Configuration);
        }
        
        /// <summary>
        /// Get the configuration for the specified user
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public ConfigurationViewModel GetUserConfiguration()
        {
            return new ConfigurationViewModel(XamarinApplicationContext.Current.GetUserConfiguration(AuthenticationContext.Current.Principal.Identity.Name));
        }

        /// <summary>
        /// Join the realm
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
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
                var serviceClientSection = XamarinApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>();
                if (serviceClientSection == null)
                {
                    serviceClientSection = new ServiceClientConfigurationSection()
                    {
                        RestClientType = typeof(RestClient)
                    };
                    XamarinApplicationContext.Current.Configuration.AddSection(serviceClientSection);
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
                    { ServiceEndpointType.AuthenticationService, "acs" }
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
                ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteSecurityRepository));
                ApplicationContext.Current.AddServiceProvider(typeof(AmiPolicyInformationService));
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
                            foreach(var ede in existingDeviceEntity)
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
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error registering device account: {0}", e);
                    throw;
                }
                return new ConfigurationViewModel(XamarinApplicationContext.Current.Configuration);
            }
            catch (PolicyViolationException ex)
            {

                this.m_tracer.TraceWarning("Policy violation exception on {0}. Will attempt again", ex.Demanded);
                // Only configure the minimum to contact the realm for authentication to continue
                var serviceClientSection = XamarinApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>();
                if (serviceClientSection == null)
                {
                    serviceClientSection = new ServiceClientConfigurationSection()
                    {
                        RestClientType = typeof(RestClient)
                    };
                    XamarinApplicationContext.Current.Configuration.AddSection(serviceClientSection);
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

                ApplicationContext.Current.AddServiceProvider(typeof(AmiPolicyInformationService));
                ApplicationContext.Current.AddServiceProvider(typeof(RemoteRepositoryService));
                ApplicationContext.Current.AddServiceProvider(typeof(RemoteSecurityRepository));

                AmiServiceClient amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

                // We get the ami options for the other configuration
                try
                {
                    var serviceOptions = amiClient.Options();

                    var option = serviceOptions.Endpoints.FirstOrDefault(o => o.ServiceType == ServiceEndpointType.AuthenticationService);

                    if (option == null)
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
                            Endpoint = option.BaseUrl.Select(o => new ServiceClientEndpoint()
                            {
                                Address = o.Replace("0.0.0.0", realmUri),
                                Timeout = 30000
                            }).ToList()
                        });

                    }

                    // Update the security binding on the temporary AMI binding
                    option = serviceOptions.Endpoints.FirstOrDefault(o => o.ServiceType == ServiceEndpointType.AdministrationIntegrationService);

                    serviceClientSection.Client[0].Binding.Security = new ServiceClientSecurity()
                    {
                        AuthRealm = realmUri,
                        Mode = option.Capabilities.HasFlag(ServiceEndpointCapabilities.BearerAuth) ? SecurityScheme.Bearer :
                                    option.Capabilities.HasFlag(ServiceEndpointCapabilities.BasicAuth) ? SecurityScheme.Basic :
                                    SecurityScheme.None,
                        CredentialProvider = option.Capabilities.HasFlag(ServiceEndpointCapabilities.BearerAuth) ? (ICredentialProvider)new TokenCredentialProvider() :
                                    option.Capabilities.HasFlag(ServiceEndpointCapabilities.BasicAuth) ?
                                    (ICredentialProvider)(option.ServiceType == ServiceEndpointType.AuthenticationService ? (ICredentialProvider)new OAuth2CredentialProvider() : new HttpBasicTokenCredentialProvider()) :
                                    null,
                        PreemptiveAuthentication = option.Capabilities != ServiceEndpointCapabilities.None
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
            XamarinApplicationContext.Current.SaveUserConfiguration(AuthenticationContext.Current.Principal.Identity.Name,
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
        /// Update the configuration of the service
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public ConfigurationViewModel UpdateConfiguration(ConfigurationViewModel configuration)
        {
            // We will be rewriting the configuration
            if (!(ApplicationContext.Current as XamarinApplicationContext).ConfigurationPersister.IsConfigured)
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
                                || o is ITwoFactorRequestService
                                || o is IAuditRepositoryService
                                || o is ISynchronizationService
                                || o is IMailMessageRepositoryService).ToArray())
                            ApplicationContext.Current.RemoveServiceProvider(idp.GetType());
                        ApplicationContext.Current.AddServiceProvider(typeof(AmiPolicyInformationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteRepositoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteSecurityRepository), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(AmiTwoFactorRequestService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteAuditRepositoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteMailRepositoryService), true);
                        break;
                    }
                case SynchronizationMode.Offline:
                    {
                        throw new NotSupportedException();
                    }
                case SynchronizationMode.Sync:
                    {
                        // Remove any references to remote storage providers
                        ApplicationContext.Current.RemoveServiceProvider(typeof(AmiPolicyInformationService), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteRepositoryService), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteSecurityRepository), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(AmiTwoFactorRequestService), true);
                        ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteAuditRepositoryService), true);

                        // Configure the selected storage provider
                        var storageProvider = StorageProviderUtil.GetProvider(configuration.Data.Provider);
                            configuration.Data.Options.Add("DataDirectory", XamarinApplicationContext.Current.ConfigurationPersister.ApplicationDataDirectory);
                            storageProvider.Configure(ApplicationContext.Current.Configuration, configuration.Data.Options);

                        // Remove all data persistence services
                        foreach (var idp in ApplicationContext.Current.GetServices().Where(o => o is IDataPersistenceService
                                || o is IPolicyInformationService
                                || o is ISecurityRepositoryService
                                || o is ITwoFactorRequestService
                                || o is IAuditRepositoryService
                                || o is ISynchronizationService
                                || o is IMailMessageRepositoryService))
                            ApplicationContext.Current.RemoveServiceProvider(idp.GetType());

                        ApplicationContext.Current.AddServiceProvider(typeof(RemoteSynchronizationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalMailService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(HdsiIntegrationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(AmiIntegrationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(AmiTwoFactorRequestService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(MailSynchronizationService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalRepositoryFactoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalRepositoryService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalSecurityRepository), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalTagPersistenceService), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(PersistenceEntitySource), true);
                        ApplicationContext.Current.AddServiceProvider(typeof(LocalCarePlanManagerService), true);

                        // Sync settings
                        var syncConfig = new SynchronizationConfigurationSection();
                        var binder = new SanteDB.Core.Model.Serialization.ModelSerializationBinder();

                        var facilityIdentifiers = configuration.Synchronization.Facilities;
                        foreach (var id in facilityIdentifiers)
                        {
                            var facility = ApplicationContext.Current.GetService<IRepositoryService<Place>>().Get(Guid.Parse(id.ToString()), Guid.Empty);

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

                                    while (filter.Contains("$"))
                                    {
                                        int spos = filter.IndexOf("$") + 1,
                                            slen = filter.IndexOf("$", spos) - spos;

                                        var varName = filter.Substring(spos, slen);
                                        var pexpr = QueryExpressionParser.BuildPropertySelector<Place>(varName.Substring(varName.IndexOf(".") + 1));

                                        // Evaluate
                                        var sval = pexpr.Compile().DynamicInvoke(facility);
                                        filter = filter.Replace($"${varName}$", sval?.ToString());
                                    }

                                    return filter;
                                }).ToList()
                            }));
                        }

                        // Synchronization resources
                        syncConfig.SynchronizationResources.Add(new SynchronizationResource()
                        {
                            ResourceAqn = "EntityRelationship",
                            Triggers = SynchronizationPullTriggerType.OnCommit
                        });

                        syncConfig.Facilities = configuration.Synchronization.Facilities;
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

            // Repository service provider
            ApplicationContext.Current.AddServiceProvider(typeof(RepositoryEntitySource), true);
            // Override application secret
            ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().ApplicationSecret = configuration.Security.ApplicationSecret;

            // Audit retention.
            ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().AuditRetention = TimeSpan.Parse(configuration.Security.AuditRetentionXml);

            if (configuration.Security.OnlySubscribedFacilities)
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().OnlySubscribedFacilities = true;

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
                        TraceWriter = new LogTraceWriter(System.Diagnostics.Tracing.EventLevel.Critical, "SanteDB")
                    },
#endif
                new TraceWriterConfiguration()
                {
                    Filter = configuration.Log.Mode,
                    InitializationData = "SanteDB",
                    TraceWriter = new FileTraceWriter(configuration.Log.Mode, "SanteDB")
                }
            };


            foreach (var i in configuration.Application.AppSettings)
                ApplicationContext.Current.ConfigurationManager.SetAppSetting(i.Key, i.Value);

            // Other sections
            foreach(var oth in configuration.OtherSections)
            {
                if(ApplicationContext.Current.ConfigurationManager.Configuration.GetSection(oth.GetType()) != null)
                    ApplicationContext.Current.ConfigurationManager.Configuration.Sections.RemoveAll(o => o.GetType() == oth.GetType());
                ApplicationContext.Current.ConfigurationManager.Configuration.AddSection(oth);
            }
            this.m_tracer.TraceInfo("Saving configuration options {0}", JsonConvert.SerializeObject(configuration));
            ApplicationContext.Current.ConfigurationPersister.Save(ApplicationContext.Current.Configuration);

            return new ConfigurationViewModel(XamarinApplicationContext.Current.Configuration);
        }
    }
}
