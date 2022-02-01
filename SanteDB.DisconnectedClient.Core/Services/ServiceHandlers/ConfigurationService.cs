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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SanteDB.Core.Http.Description;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Query;
using SanteDB.Messaging.AMI.Client;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Interop.HDSI;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.DisconnectedClient.Diagnostics;
using SanteDB.DisconnectedClient.Http;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services.Attributes;
using SanteDB.DisconnectedClient.Services.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Services;
using SanteDB.Core.Model.Entities;
using System.Data;
using SanteDB.DisconnectedClient.Security.Remote;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Caching;
using SanteDB.DisconnectedClient.Interop.AMI;
using SanteDB.DisconnectedClient.Security.Audit;
using System.Net;
using SanteDB.Core.Interop;
using SanteDB.Core.Http;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using System.Linq.Expressions;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Services.Local;
using SanteDB.DisconnectedClient.Data;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Asn1.Pkcs;
using System.Security.Cryptography.X509Certificates;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using System.Reflection;
using SanteDB.DisconnectedClient.Services.Remote;
using SanteDB.DisconnectedClient.Mail;
using SanteDB.Core.Security;

namespace SanteDB.DisconnectedClient.Services.ServiceHandlers
{
    /// <summary>
    /// Configuration view model
    /// </summary>
    [JsonObject]
    public class ConfigurationViewModel
    {
        public ConfigurationViewModel()
        {

        }

        /// <summary>
        /// Get the type
        /// </summary>
        [JsonProperty("$type")]
        public String Type { get { return "configuration"; } }

        /// <summary>
        /// Return true if configured
        /// </summary>
        [JsonProperty("isConfigured")]
        public bool IsConfigured { get => (ApplicationContext.Current as ApplicationContext).ConfigurationManager.IsConfigured; }

        /// <summary>
        /// Configuation
        /// </summary>
        /// <param name="config"></param>
        public ConfigurationViewModel(SanteDBConfiguration config)
        {
            if (config == null) return;
            this.RealmName = config.GetSection<SecurityConfigurationSection>()?.Domain;
            this.Security = config.GetSection<SecurityConfigurationSection>();
            this.Data = config.GetSection<DataConfigurationSection>();
            this.Applet = config.GetSection<AppletConfigurationSection>();
            this.Application = config.GetSection<ApplicationConfigurationSection>();
            this.Log = config.GetSection<DiagnosticsConfigurationSection>();
            this.Network = config.GetSection<ServiceClientConfigurationSection>();
            this.Synchronization = config.GetSection<SynchronizationConfigurationSection>();
        }
        /// <summary>
        /// Security section
        /// </summary>
        [JsonProperty("security")]
        public SecurityConfigurationSection Security { get; set; }
        /// <summary>
        /// Realm name
        /// </summary>
        [JsonProperty("realmName")]
        public String RealmName { get; set; }
        /// <summary>
        /// Data config
        /// </summary>
        [JsonProperty("data")]
        public DataConfigurationSection Data { get; set; }
        /// <summary>
        /// Gets or sets applet
        /// </summary>
        [JsonProperty("applet")]
        public AppletConfigurationSection Applet { get; set; }
        /// <summary>
        /// Gets or sets application
        /// </summary>
        [JsonProperty("application")]
        public ApplicationConfigurationSection Application { get; set; }
        /// <summary>
        /// Log
        /// </summary>
        [JsonProperty("log")]
        public DiagnosticsConfigurationSection Log { get; set; }
        /// <summary>
        /// Gets or sets the network
        /// </summary>
        [JsonProperty("network")]
        public ServiceClientConfigurationSection Network { get; set; }
        /// <summary>
        /// Synchronization
        /// </summary>
        [JsonProperty("sync")]
        public SynchronizationConfigurationSection Synchronization { get; set; }
    }

    /// <summary>
    /// View model for provider
    /// </summary>
    [JsonObject]
    public class StorageProviderViewModel
    {
        /// <summary>
        /// The invariant name
        /// </summary>
        [JsonProperty("invariant")]
        public string Invariant { get; set; }

        /// <summary>
        /// The property name
        /// </summary>
        [JsonProperty("name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the options
        /// </summary>
        [JsonProperty("options")]
        public Dictionary<String, ConfigurationOptionType> Options { get; set; }
    }

    /// <summary>
    /// Restful service
    /// </summary>
    [RestService("/__ami")]
    public class ConfigurationService
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(ConfigurationService));

        /// <summary>
        /// Get a list of all subscription definitions defined in the loaded applets
        /// </summary>
        [RestOperation(UriPath = "/subscriptionDefinition", Method = "GET", FaultProvider = nameof(ConfigurationFaultProvider))]
        [return: RestMessage(RestMessageFormat.Json)]
        public List<AppletSubscriptionDefinition> GetSubscriptionDefinitions()
        {
            return ApplicationContext.Current.GetService<IAppletManagerService>().Applets.SelectMany(o => o.SubscriptionDefinition).ToList();
        }

        /// <summary>
        /// Get the data storage provider
        /// </summary>
        [RestOperation(UriPath = "/dbp", Method = "GET", FaultProvider = nameof(ConfigurationFaultProvider))]
        [return: RestMessage(RestMessageFormat.Json)]
        public List<StorageProviderViewModel> GetDataStorageProviders()
        {
            return StorageProviderUtil.GetProviders().Select(o => new StorageProviderViewModel()
            {
                Invariant = o.Invariant,
                Name = o.Name,
                Options = o.Options
            }).ToList();
        }

        /// <summary>
        /// Gets the currently authenticated user's configuration
        /// </summary>
        [RestOperation(UriPath = "/configuration/user", Method = "GET", FaultProvider = nameof(ConfigurationFaultProvider))]
        [return: RestMessage(RestMessageFormat.Json)]
        [Demand(PermissionPolicyIdentifiers.Login)]
        public ConfigurationViewModel GetUserConfiguration()
        {
            String userId = MiniHdsiServer.CurrentContext.Request.QueryString["_id"] ?? AuthenticationContext.Current.Principal.Identity.Name;
            return new ConfigurationViewModel(ApplicationContext.Current.GetUserConfiguration(userId));

        }

        /// <summary>
        /// Gets the currently authenticated user's configuration
        /// </summary>
        [RestOperation(UriPath = "/configuration/user", Method = "POST", FaultProvider = nameof(ConfigurationFaultProvider))]
        public void SaveUserConfiguration([RestMessage(RestMessageFormat.Json)]ConfigurationViewModel model)
        {
            String userId = MiniHdsiServer.CurrentContext.Request.QueryString["_id"] ?? AuthenticationContext.Current.Principal.Identity.Name;
            ApplicationContext.Current.SaveUserConfiguration(userId,
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
        /// Gets the specified forecast
        /// </summary>
        [RestOperation(UriPath = "/configuration", Method = "GET", FaultProvider = nameof(ConfigurationFaultProvider))]
        [return: RestMessage(RestMessageFormat.Json)]
        public ConfigurationViewModel GetConfiguration()
        {
            return new ConfigurationViewModel(ApplicationContext.Current.Configuration);
        }

        /// <summary>
        /// Save configuration
        /// </summary>
        [RestOperation(UriPath = "/configuration", Method = "POST", FaultProvider = nameof(ConfigurationFaultProvider))]
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        [return: RestMessage(RestMessageFormat.Json)]
        public ConfigurationViewModel SaveConfiguration([RestMessage(RestMessageFormat.Json)]JObject optionObject)
        {
            // Clean up join realm stuff
            ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.RemoveAll(o => o == typeof(AmiPolicyInformationService).AssemblyQualifiedName);
            ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.RemoveAll(o => o == typeof(RemoteRepositoryService).AssemblyQualifiedName);
            ApplicationContext.Current.Configuration.Sections.RemoveAll(o => o is SynchronizationConfigurationSection);

            ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().AppletSolution = optionObject["applet"]?["solution"]?.ToString();
            ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().AutoUpdateApplets = optionObject["applet"]?["autoUpdate"]?.Value<Boolean>() == true;

            ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(LocalAuditService).AssemblyQualifiedName);

            // Data mode
            switch (optionObject["sync"]["mode"].Value<String>())
            {
                case "online":
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiPolicyInformationService).AssemblyQualifiedName);
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(RemoteRepositoryService).AssemblyQualifiedName);
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(RemoteSecurityRepository).AssemblyQualifiedName);
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiTwoFactorRequestService).AssemblyQualifiedName);

                    break;
                case "offline":
                    {
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.RemoveAll(o => o == typeof(OAuthIdentityProvider).AssemblyQualifiedName || o == typeof(HttpBasicIdentityProvider).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(DefaultPolicyDecisionService).AssemblyQualifiedName);
                        var storageProvider = StorageProviderUtil.GetProvider(optionObject["data"]["provider"].Value<String>());
                        storageProvider.Configure(ApplicationContext.Current.Configuration, ApplicationContext.Current.ConfigurationManager.ApplicationDataDirectory, optionObject["data"]["options"].ToObject<Dictionary<String, Object>>());

                        break;
                    }
                case "sync":
                    {
                        var storageProvider = StorageProviderUtil.GetProvider(optionObject["data"]["provider"].Value<String>());
                        storageProvider.Configure(ApplicationContext.Current.Configuration, ApplicationContext.Current.ConfigurationManager.ApplicationDataDirectory, optionObject["data"]["options"].ToObject<Dictionary<String, Object>>());

                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(RemoteSynchronizationService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(LocalMailService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(HdsiIntegrationService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiIntegrationService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiTwoFactorRequestService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(MailSynchronizationService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(DefaultPolicyDecisionService).AssemblyQualifiedName);

                        // Sync settings
                        var syncConfig = new SynchronizationConfigurationSection();
                        var binder = new SanteDB.Core.Model.Serialization.ModelSerializationBinder();

                        var facilityIdentifiers = optionObject["sync"]["subscribe"].ToArray();
                        foreach (var id in facilityIdentifiers)
                        {
                            var facility = ApplicationContext.Current.GetService<IPlaceRepositoryService>().Get(Guid.Parse(id.ToString()), Guid.Empty);

                            // Subscription data
                            // TODO: Check if mode is ALL
                            var subscriptions = optionObject["sync"]["resource"].ToArray();
                            syncConfig.SynchronizationResources.AddRange(subscriptions.Select(sub => new SynchronizationResource()
                            {
                                Name = sub["name"].ToString(),
                                ResourceAqn = sub["resource"].ToString(),
                                Triggers = (SynchronizationPullTriggerType)Enum.Parse(typeof(SynchronizationPullTriggerType), sub["trigger"].ToString()),
                                Always = Boolean.Parse(sub["ignoreModifiedOn"].ToString()),
                                Filters = sub["filter"].ToArray().Select(f =>
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

                        syncConfig.SynchronizationResources = syncConfig.SynchronizationResources.GroupBy(o => o.Name + o.Triggers.ToString())
                            .Select(g =>
                            {
                                var retVal = g.First();
                                retVal.Filters = g.SelectMany(r => r.Filters).Distinct().ToList();
                                return retVal;
                            })
                            .ToList();
                        if (optionObject["sync"]["pollInterval"].Value<String>() != "00:00:00")
                            syncConfig.PollIntervalXml = optionObject["sync"]["pollInterval"].Value<String>();
                        ApplicationContext.Current.Configuration.Sections.Add(syncConfig);

                        break;
                    }
            }


            // Password hashing
            switch (optionObject["security"]["hasher"].Value<String>())
            {
                case "SHA256PasswordHasher":
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SHA256PasswordHasher).AssemblyQualifiedName);
                    break;
                case "SHAPasswordHasher":
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SHAPasswordHasher).AssemblyQualifiedName);
                    break;
                case "PlainTextPasswordHasher":
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(PlainTextPasswordHasher).AssemblyQualifiedName);
                    break;
            }

            // Override application secret
            ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().ApplicationSecret = optionObject["security"]?["client_secret"]?.Value<String>() ?? ApplicationContext.Current.Application.ApplicationSecret;

            // Audit retention.
            ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().AuditRetention = TimeSpan.Parse(optionObject["security"]["auditRetention"].Value<String>());

            if (optionObject["security"]?["onlySubscribedAuth"]?.Value<Boolean>() == true)
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().OnlySubscribedFacilities = true;

            // Proxy
            if (optionObject["network"]["useProxy"].Value<Boolean>())
                ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>().ProxyAddress = optionObject["network"]["proxyAddress"].Value<String>();

            var optimize = optionObject["network"]["optimize"].Value<String>();
            OptimizationMethod method = OptimizationMethod.Gzip;
            if (!String.IsNullOrEmpty(optimize))
                switch (optimize)
                {
                    case "lzma":
                        method = OptimizationMethod.Lzma;
                        break;
                    case "bzip2":
                        method = OptimizationMethod.Bzip2;
                        break;
                    case "deflate":
                        method = OptimizationMethod.Deflate;
                        break;
                    case "gzip":
                        method = OptimizationMethod.Gzip;
                        break;
                    case "off":
                        method = OptimizationMethod.None;
                        break;
                }

            foreach (var itm in ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>().Client)
                if (itm.Binding.Optimize)
                    itm.Binding.OptimizationMethod = method;

            // Log settings
            var logSettings = ApplicationContext.Current.Configuration.GetSection<DiagnosticsConfigurationSection>();
            logSettings.TraceWriter = new System.Collections.Generic.List<TraceWriterConfiguration>()
            {
#if DEBUG
                new TraceWriterConfiguration () {
                        Filter = System.Diagnostics.Tracing.EventLevel.Critical,
                        InitializationData = "SanteDB",
                        TraceWriter = new LogTraceWriter (System.Diagnostics.Tracing.EventLevel.Critical, "SanteDB")
                    },
#endif
                new TraceWriterConfiguration()
                {
                    Filter = (EventLevel)Enum.Parse(typeof(EventLevel), optionObject["log"]["mode"].Value<String>()),
                    InitializationData = "SanteDB",
                    TraceWriter = new FileTraceWriter((EventLevel)Enum.Parse(typeof(EventLevel), optionObject["log"]["mode"].Value<String>()), "SanteDB")

                }

            };


            this.m_tracer.TraceInfo("Saving configuration options {0}", optionObject);
            ApplicationContext.Current.ConfigurationManager.Save();

            return new ConfigurationViewModel(ApplicationContext.Current.Configuration);
        }

        /// <summary>
        /// Join a realm
        /// </summary>
        /// <param name="configData"></param>
        [RestOperation(UriPath = "/configuration/realm", Method = "POST", FaultProvider = nameof(ConfigurationFaultProvider))]
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        [return: RestMessage(RestMessageFormat.Json)]
        public ConfigurationViewModel JoinRealm([RestMessage(RestMessageFormat.Json)]JObject configData)
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

            }
            this.m_tracer.TraceInfo("Joining {0}", realmUri);

            // Stage 1 - Demand access admin policy
            try
            {

                new PolicyPermission(PermissionState.Unrestricted, PermissionPolicyIdentifiers.UnrestrictedAdministration).Demand();

                // We're allowed to access server admin!!!! Yay!!!
                // We're goin to conigure the realm settings now (all of them)
                var serviceClientSection = ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>();
                if (serviceClientSection == null)
                {
                    serviceClientSection = new ServiceClientConfigurationSection()
                    {
                        RestClientType = typeof(RestClient)
                    };
                    ApplicationContext.Current.Configuration.Sections.Add(serviceClientSection);
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

                //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new AmiPolicyInformationService());
                //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new RemoteRepositoryService());
                ApplicationContext.Current.GetService<IDataPersistenceService<Concept>>().Query(o => o.ConceptSets.Any(s => s.Key == ConceptSetKeys.AddressComponentType));
                EntitySource.Current = new EntitySource(new ConfigurationEntitySource());
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
                    // Create application user
                    var role = amiClient.GetRole("SYNCHRONIZERS");

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
                        amiClient.CreateDeviceEntity(new DeviceEntity()
                        {
                            SecurityDevice = newDevice.Entity,
                            StatusConceptKey = StatusKeys.Active,
                            ManufacturerModelName = Environment.MachineName,
                            OperatingSystemName = Environment.OSVersion.ToString(),
                            Names = new List<EntityName>()
                                {
                                    new EntityName(NameUseKeys.Assigned, deviceName)
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
                            amiClient.UpdateDevice(existingDevice.CollectionItem.OfType<SecurityDeviceInfo>().First().Entity.Key.Value, new SecurityDeviceInfo(new SanteDB.Core.Model.Security.SecurityDevice()
                            {

                                UpdatedTime = DateTime.Now,
                                Name = deviceName,
                                DeviceSecret = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret
                            })
                            {
                                Policies = role.Policies
                            });
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error registering device account: {0}", e);
                    throw;
                }
                return new ConfigurationViewModel(ApplicationContext.Current.Configuration);
            }
            catch (PolicyViolationException ex)
            {
                this.m_tracer.TraceWarning("Policy violation exception on {0}. Will attempt again", ex.Demanded);
                // Only configure the minimum to contact the realm for authentication to continue
                var serviceClientSection = ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>();
                if (serviceClientSection == null)
                {
                    serviceClientSection = new ServiceClientConfigurationSection()
                    {
                        RestClientType = typeof(RestClient)
                    };
                    ApplicationContext.Current.Configuration.Sections.Add(serviceClientSection);
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


                AmiServiceClient amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

                // We get the ami options for the other configuration
                var serviceOptions = amiClient.Options();

                var option = serviceOptions.Endpoints.FirstOrDefault(o => o.ServiceType == ServiceEndpointType.AuthenticationService);

                if (option == null)
                {
                    //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new HttpBasicIdentityProvider());
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(HttpBasicIdentityProvider).AssemblyQualifiedName);
                }
                else
                {
                    //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new OAuthIdentityProvider());
                    //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new OAuthDeviceIdentityProvider());
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(OAuthIdentityProvider).AssemblyQualifiedName);
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(OAuthDeviceIdentityProvider).AssemblyQualifiedName);
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

                //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.RemoveAll(o => o is AmiPolicyInformationService);
                //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.RemoveAll(o => o is RemoteRepositoryService);

                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = null;
                throw new UnauthorizedAccessException();
            }
            catch (Exception e)
            {
                //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.RemoveAll(o => o is AmiPolicyInformationService);
                //ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.RemoveAll(o => o is RemoteRepositoryService);
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = null;
                this.m_tracer.TraceError("Error joining context: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Handle a fault
        /// </summary>
        public ErrorResult ConfigurationFaultProvider(Exception e)
        {
            return new ErrorResult()
            {
                Error = e.Message,
                ErrorDescription = e.InnerException?.Message,
                ErrorType = e.GetType().Name
            };
        }

        /// <summary>
        /// Configuration eneiity source
        /// </summary>
        private class ConfigurationEntitySource : IEntitySourceProvider
        {
            /// <summary>
            /// Get the specified object
            /// </summary>
            public TObject Get<TObject>(Guid? key) where TObject : IdentifiedData, new()
            {
                if (typeof(Concept).IsAssignableFrom(typeof(TObject)))
                    return ApplicationContext.Current.GetService<IDataPersistenceService<TObject>>()?.Get(key.Value);
                return null;
            }

            /// <summary>
            /// Get specified version
            /// </summary>
            /// <typeparam name="TObject"></typeparam>
            /// <param name="key"></param>
            /// <param name="versionKey"></param>
            /// <returns></returns>
            public TObject Get<TObject>(Guid? key, Guid? versionKey) where TObject : IdentifiedData, IVersionedEntity, new()
            {
                if (typeof(Concept).IsAssignableFrom(typeof(TObject)))
                    return ApplicationContext.Current.GetService<IDataPersistenceService<TObject>>()?.Get(key.Value);
                return null;
            }


            public IEnumerable<TObject> GetRelations<TObject>(Guid? sourceKey) where TObject : IdentifiedData, ISimpleAssociation, new()
            {
                return ApplicationContext.Current.GetService<IDataPersistenceService<TObject>>()?.Query(o => o.SourceEntityKey == sourceKey) ?? new List<TObject>();
            }

            public IEnumerable<TObject> GetRelations<TObject>(Guid? sourceKey, decimal? sourceVersionSequence) where TObject : IdentifiedData, IVersionedAssociation, new()
            {
                return ApplicationContext.Current.GetService<IDataPersistenceService<TObject>>()?.Query(o => o.SourceEntityKey == sourceKey) ?? new List<TObject>();
            }

            public IEnumerable<TObject> Query<TObject>(Expression<Func<TObject, bool>> query) where TObject : IdentifiedData, new()
            {
                return ApplicationContext.Current.GetService<IDataPersistenceService<TObject>>()?.Query(query) ?? new List<TObject>();
            }
        }
    }
}
