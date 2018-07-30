/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 * 
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
 * Date: 2017-9-1
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SanteDB.Core.Http.Description;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Query;
using SanteDB.Messaging.AMI.Client;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Data;
using SanteDB.DisconnectedClient.Core.Diagnostics;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.Core.Interop.HDSI;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Synchronization;
using SanteDB.DisconnectedClient.Xamarin.Diagnostics;
using SanteDB.DisconnectedClient.Xamarin.Http;
using SanteDB.DisconnectedClient.Xamarin.Security;
using SanteDB.DisconnectedClient.Xamarin.Services.Attributes;
using SanteDB.DisconnectedClient.Xamarin.Services.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.Core.Model.Entities;
using System.Data;
using SanteDB.DisconnectedClient.Core.Security.Remote;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Caching;
using SanteDB.DisconnectedClient.Core.Interop.AMI;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using System.Net;
using SanteDB.Core.Interop;
using SanteDB.Core.Http;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using System.Linq.Expressions;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Alerting;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite;
using SanteDB.DisconnectedClient.SQLite.Synchronization;
using SanteDB.DisconnectedClient.SQLite.Warehouse;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.SQLite.Security;
using SanteDB.DisconnectedClient.Core.Services.Impl;
using SanteDB.DisconnectedClient.Xamarin.Data;
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

namespace SanteDB.DisconnectedClient.Xamarin.Services.ServiceHandlers
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
        [Demand(PolicyIdentifiers.Login)]
        public ConfigurationViewModel GetUserConfiguration()
        {
            String userId = MiniHdsiServer.CurrentContext.Request.QueryString["_id"] ?? AuthenticationContext.Current.Principal.Identity.Name;
            return new ConfigurationViewModel(XamarinApplicationContext.Current.GetUserConfiguration(userId));

        }

        /// <summary>
        /// Gets the currently authenticated user's configuration
        /// </summary>
        [RestOperation(UriPath = "/configuration/user", Method = "POST", FaultProvider = nameof(ConfigurationFaultProvider))]
        public void SaveUserConfiguration([RestMessage(RestMessageFormat.Json)]ConfigurationViewModel model)
        {
            String userId = MiniHdsiServer.CurrentContext.Request.QueryString["_id"] ?? AuthenticationContext.Current.Principal.Identity.Name;
            XamarinApplicationContext.Current.SaveUserConfiguration(userId,
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
            return new ConfigurationViewModel(XamarinApplicationContext.Current.Configuration);
        }

        /// <summary>
        /// Save configuration
        /// </summary>
        [RestOperation(UriPath = "/configuration", Method = "POST", FaultProvider = nameof(ConfigurationFaultProvider))]
        [Demand(PolicyIdentifiers.AccessClientAdministrativeFunction)]
        [return: RestMessage(RestMessageFormat.Json)]
        public ConfigurationViewModel SaveConfiguration([RestMessage(RestMessageFormat.Json)]JObject optionObject)
        {
            // Clean up join realm stuff
            ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.RemoveAll(o => o == typeof(AmiPolicyInformationService).AssemblyQualifiedName);
            ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.RemoveAll(o => o == typeof(HdsiPersistenceService).AssemblyQualifiedName);
            ApplicationContext.Current.Configuration.Sections.RemoveAll(o => o is SynchronizationConfigurationSection);


            ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(LocalAuditService).AssemblyQualifiedName);

            // Data mode
            switch (optionObject["sync"]["mode"].Value<String>())
            {
                case "online":
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.RemoveAll(o => o == typeof(SQLitePolicyInformationService).AssemblyQualifiedName);
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiPolicyInformationService).AssemblyQualifiedName);

                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(HdsiPersistenceService).AssemblyQualifiedName);
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiTwoFactorRequestService).AssemblyQualifiedName);

                    break;
                case "offline":
                    {
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.RemoveAll(o => o == typeof(OAuthIdentityProvider).AssemblyQualifiedName || o == typeof(HttpBasicIdentityProvider).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(LocalPolicyDecisionService).AssemblyQualifiedName);
                        var storageProvider = StorageProviderUtil.GetProvider(optionObject["data"]["provider"].Value<String>());
                        storageProvider.Configure(ApplicationContext.Current.Configuration, optionObject["data"]["options"].ToObject<Dictionary<String, Object>>());

                        break;
                    }
                case "sync":
                    {
                        var storageProvider = StorageProviderUtil.GetProvider(optionObject["data"]["provider"].Value<String>());
                        storageProvider.Configure(ApplicationContext.Current.Configuration, optionObject["data"]["options"].ToObject<Dictionary<String, Object>>());

                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(RemoteSynchronizationService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(LocalMailService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(HdsiIntegrationService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiIntegrationService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiTwoFactorRequestService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(MailSynchronizationService).AssemblyQualifiedName);
                        ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(LocalPolicyDecisionService).AssemblyQualifiedName);

                        // Sync settings
                        var syncConfig = new SynchronizationConfigurationSection();
                        var binder = new SanteDB.Core.Model.Serialization.ModelSerializationBinder();

                        var facilityId = optionObject["sync"]["subscribe"].ToString();
                        var facility = ApplicationContext.Current.GetService<IPlaceRepositoryService>().Get(Guid.Parse(facilityId), Guid.Empty);
                        var facilityAddress = facility.LoadCollection<EntityAddress>("Addresses").FirstOrDefault();
                        var facilityState = facilityAddress?.Value(AddressComponentKeys.State);
                        var facilityCounty = facilityAddress?.Value(AddressComponentKeys.County);

                        // TODO: Customize this and clean it up ... It is very hackish
                        foreach (var res in new String[] {
                        "ConceptSet",
                        "AssigningAuthority",
                        "IdentifierType",
                        "TemplateDefinition",
                        "ExtensionType",
                        "ConceptClass",
                        "Concept",
                        "Material",
                        "Place",
                        "PlaceMe",
                        "Organization",
                        "UserEntity",
                        "UserEntityMe",
                        "PlaceOther",
                        "Provider",
                        "ManufacturedMaterial",
                        "ManufacturedMaterialMe",
                        "Patient",
                        "Person",
                        "PatientEncounter",
                        "PatientEncounterMe",
                        "SubstanceAdministration",
                        "CodedObservation",
                        "QuantityObservation",
                        "TextObservation",
                        "Act"})
                        {
                            var syncSetting = new SynchronizationResource()
                            {
                                ResourceAqn = res,
                                Triggers = new String[] { "Person", "Act", "SubstanceAdministration", "QuantityObservation", "CodedObservation", "TextObservation", "PatientEncounter" }.Contains(res) ? SynchronizationPullTriggerType.Always :
                                    SynchronizationPullTriggerType.OnNetworkChange | SynchronizationPullTriggerType.OnStart
                            };

                            // Subscription
                            if (optionObject["data"]["sync"]["subscribe"] == null)
                            {
                                var efield = typeof(EntityClassKeys).GetField(res);

                                if (res == "Person")
                                {
                                    syncSetting.Filters.Add("classConcept=" + EntityClassKeys.Patient);
                                    syncSetting.Filters.Add("classConcept=" + EntityClassKeys.Person + "&relationship.source.classConcept=" + EntityClassKeys.Patient);
                                }
                                else if (res == "Act")
                                {
                                    syncSetting.Filters.Add("classConcept=" + ActClassKeys.AccountManagement);
                                    syncSetting.Filters.Add("classConcept=" + ActClassKeys.Supply);
                                }
                                else if (res == "EntityRelationship" || res == "UserEntityMe") continue;
                                else if (efield != null && res != "Place")
                                    syncSetting.Filters.Add("classConcept=" + efield.GetValue(null).ToString());
                            }
                            else
                            { // Only interested in a few facilities
                                if (!syncConfig.Facilities.Contains(facilityId))
                                    syncConfig.Facilities.Add(facilityId);

                                switch (res)
                                {
                                    case "UserEntity":
                                    case "Provider":
                                        syncSetting.Name = "locale.sync.resource.Provider";
                                        if (syncSetting.Filters.Count == 0)
                                        {
                                            // All users and providers for stuff I'm interested in the area
                                            syncSetting.Filters.Add("participation[Location|EntryLocation|Destination|InformationRecipient|PrimaryInformationRecipient].player=" + facilityId + "&_exclude=relationship&_exclude=participation");
                                            // All users or providers who are involved in acts this facility is subscribed to
                                            syncSetting.Filters.Add("participation.source.participation.player=" + facilityId + "&_exclude=relationship&_exclude=participation");
                                        }
                                        break;
                                    case "Patient":
                                        syncSetting.Name = "locale.sync.resource.Patient";
                                        syncSetting.Filters.Add("relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation].target=" + facilityId);
                                        break;
                                    case "Person":
                                        syncSetting.Name = "locale.sync.resource.Person";
                                        syncSetting.Filters.Add("classConcept=" + EntityClassKeys.Person + "&relationship.source.classConcept=" + EntityClassKeys.Patient + "&relationship.source.relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation].target=" + facilityId);
                                        break;
                                    case "Act":
                                        syncSetting.Name = "locale.sync.resource.Act.other";
                                        syncSetting.Filters.Add("classConcept=!" + ActClassKeys.SubstanceAdministration +
                                            "&classConcept=!" + ActClassKeys.Observation +
                                            "&classConcept=!" + ActClassKeys.Encounter +
                                            "&classConcept=!" + ActClassKeys.Procedure +
                                            "&participation[Destination|Location].player=" + facilityId + "&_expand=relationship&_expand=participation");
                                        //syncSetting.Filters.Add("classConcept=" + ActClassKeys.Supply + "&participation[Location].player=" + itm + "&_expand=relationship&_expand=participation");
                                        //syncSetting.Filters.Add("classConcept=" + ActClassKeys.AccountManagement + "&participation[Location].player=" + itm + "&_expand=relationship&_expand=participation");
                                        //syncSetting.Filters.Add("participation[EntryLocation].player=" + itm + "&_expand=relationship&_expand=participation");
                                        break;
                                    case "UserEntityMe":
                                        syncSetting.Name = "locale.sync.resource.UserEntity.my";
                                        syncSetting.ResourceAqn = "UserEntity";
                                        syncSetting.Triggers = SynchronizationPullTriggerType.Always;
                                        syncSetting.Filters.Add("relationship[DedicatedServiceDeliveryLocation].target=" + facilityId + "&_expand=relationship&_expand=participation");
                                        break;
                                    case "SubstanceAdministration":
                                    case "QuantityObservation":
                                    case "CodedObservation":
                                    case "TextObservation":
                                    case "PatientEncounter":

                                        // I want all stuff for patients in my catchment
                                        syncSetting.Filters.Add("participation[RecordTarget].player.relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation].target=" + facilityId);
                                        // I want all stuff for my facility for patients which are not assigned to me
                                        syncSetting.Filters.Add("participation[Location|InformationRecipient|EntryLocation].player=" + facilityId + "&participation[RecordTarget].player.relationship[DedicatedServiceDeliveryLocation].target=!" + facilityId + "&participation[RecordTarget].player.relationship[IncidentalServiceDeliveryLocation].target=!" + facilityId);
                                        // All stuff that is happening out of my facility for any patient associated with me
                                        //syncSetting.Filters.Add("participation[Location|InformationRecipient|EntryLocation].player=!" + itm + "&participation[RecordTarget].player.relationship[DedicatedServiceDeliveryLocation|IncidentalServiceDeliveryLocation].target=" + itm);
                                        //syncSetting.Filters.Add("participation[Location].player=!" + itm + "&participation[RecordTarget].player.relationship[IncidentalServiceDeliveryLocation].target=" + itm);
                                        //syncSetting.Filters.Add("participation[Location].player=" + itm + "&participation[RecordTarget].player.relationship[DedicatedServiceDeliveryLocation].target=!" + itm + "&_expand =relationship&_expand=participation");
                                        break;
                                    case "PatientEncounterMe":
                                        syncSetting.Name = "locale.sync.resource.PatientEncounter.my";
                                        syncSetting.Filters.Add("participation[RecordTarget].source.participation[Location].player=" + facilityId + "&participation[RecordTarget].source.statusConcept=" + StatusKeys.Active + "&participation[RecordTarget].source.classConcept=" + ActClassKeys.Encounter);
                                        syncSetting.ResourceAqn = "Person";
                                        syncSetting.Triggers = SynchronizationPullTriggerType.PeriodicPoll;
                                        break;
                                    case "Place":
                                        if (facilityState != null)
                                        {
                                            syncSetting.Name = "locale.sync.resource.Place.state";

                                            // all SDL in my county
                                            syncSetting.Filters.Add("classConcept=" + EntityClassKeys.ServiceDeliveryLocation + "&address.component[County].value=" + facilityCounty + "&_exclude=relationship&_exclude=participation");
                                            // all places in my county
                                            syncSetting.Filters.Add("classConcept=!" + EntityClassKeys.ServiceDeliveryLocation + "&address.component[County].value=" + facilityCounty + "&relationship[DedicatedServiceDeliveryLocation].target=!" + facilityId + "&_exclude=relationship");
                                        }
                                        else if (facilityCounty != null)
                                        {
                                            syncSetting.Name = "locale.sync.resource.Place.county";
                                            // all sdl in my state
                                            syncSetting.Filters.Add("classConcept=" + EntityClassKeys.ServiceDeliveryLocation + "&address.component[State].value=" + facilityState + "&_exclude=relationship&_exclude=participation");
                                            // all places in my state
                                            syncSetting.Filters.Add("classConcept=!" + EntityClassKeys.ServiceDeliveryLocation + "&address.component[State].value=" + facilityState + "&relationship[DedicatedServiceDeliveryLocation].target=!" + facilityId + "&_exclude=relationship");
                                        }
                                        else
                                        {
                                            syncSetting.Name = "locale.sync.resource.Place.all";

                                            syncSetting.Filters.Add("classConcept=" + EntityClassKeys.ServiceDeliveryLocation + "&_exclude=relationship&_exclude=participation");
                                            syncSetting.Filters.Add("classConcept=!" + EntityClassKeys.ServiceDeliveryLocation + "&relationship[DedicatedServiceDeliveryLocation].target=!" + facilityId + "&_exclude=relationship");
                                        }
                                        // all places assigned to me
                                        syncSetting.Filters.Add("classConcept=!" + EntityClassKeys.ServiceDeliveryLocation + "&relationship[DedicatedServiceDeliveryLocation].target=" + facilityId);
                                        break;
                                    case "PlaceOther":

                                        syncSetting.ResourceAqn = "Place";
                                        syncSetting.Triggers = SynchronizationPullTriggerType.PeriodicPoll;
                                        if (facilityState != null)
                                        {
                                            syncSetting.Name = "locale.sync.resource.Place.outOfState";

                                            // all SDL in my county
                                            syncSetting.Filters.Add("classConcept=" + EntityClassKeys.ServiceDeliveryLocation + "&address.component[County].value=!" + facilityCounty + "&_exclude=relationship&_exclude=participation");
                                            // all places in my county
                                            syncSetting.Filters.Add("classConcept=!" + EntityClassKeys.ServiceDeliveryLocation + "&address.component[County].value=!" + facilityCounty + "&relationship[DedicatedServiceDeliveryLocation].target=!" + facilityId + "&_exclude=relationship");
                                        }
                                        else if (facilityCounty != null)
                                        {
                                            syncSetting.Name = "locale.sync.resource.Place.outOfCounty";
                                            // all sdl in my state
                                            syncSetting.Filters.Add("classConcept=" + EntityClassKeys.ServiceDeliveryLocation + "&address.component[State].value=!" + facilityState + "&_exclude=relationship&_exclude=participation");
                                            // all places in my state
                                            syncSetting.Filters.Add("classConcept=!" + EntityClassKeys.ServiceDeliveryLocation + "&address.component[State].value=!" + facilityState + "&relationship[DedicatedServiceDeliveryLocation].target=!" + facilityId + "&_exclude=relationship");
                                        }
                                        else
                                            syncSetting = null;
                                        break;
                                    case "PlaceMe":
                                        syncSetting.Name = "locale.sync.resource.Place.my";

                                        syncSetting.ResourceAqn = "Place";
                                        syncSetting.Triggers = SynchronizationPullTriggerType.Always;
                                        syncSetting.Filters.Add("id=" + facilityId);
                                        syncSetting.Always = true;
                                        break;
                                    case "Material":
                                        syncSetting.Name = "locale.sync.resource.Material";

                                        if (syncSetting.Filters.Count == 0)
                                            syncSetting.Filters.Add("classConcept=" + EntityClassKeys.Material);
                                        break;
                                    case "ManufacturedMaterial":
                                        syncSetting.Name = "locale.sync.resource.ManufacturedMaterial";

                                        if (syncSetting.Filters.Count == 0)
                                            syncSetting.Filters.Add("classConcept=" + EntityClassKeys.ManufacturedMaterial);
                                        break;
                                    case "ManufacturedMaterialMe":
                                        syncSetting.Name = "locale.sync.resource.ManufacturedMaterial.my";
                                        syncSetting.ResourceAqn = "ManufacturedMaterial";
                                        syncSetting.Triggers = SynchronizationPullTriggerType.PeriodicPoll;
                                        // Any materials involved in an act assigned to me
                                        syncSetting.Filters.Add("participation[Consumable].source.participation[Location|Destination].player=" + facilityId);
                                        // Any materials I own
                                        syncSetting.Filters.Add("relationship[OwnedEntity].source=" + facilityId);
                                        break;

                                }
                            }

                            // Assignable from
                            //if (typeof(BaseEntityData).IsAssignableFrom(binder.BindToType(typeof(BaseEntityData).Assembly.FullName, res)))
                            //{
                            //    for (int i = 0; i < syncSetting.Filters.Count; i++)
                            //        syncSetting.Filters[i] += "&obsoletionTime=null";
                            //    if (syncSetting.Filters.Count == 0)
                            //        syncSetting.Filters.Add("obsoletionTime=null");
                            //}

                            // TODO: Patient registration <> facility

                            if (syncSetting != null)
                                syncConfig.SynchronizationResources.Add(syncSetting);
                        }
                        syncConfig.SynchronizationResources.Add(new SynchronizationResource()
                        {
                            ResourceAqn = "EntityRelationship",
                            Triggers = SynchronizationPullTriggerType.OnCommit
                        });
                        if (optionObject["data"]["sync"]["pollInterval"].Value<String>() != "00:00:00")
                            syncConfig.PollIntervalXml = optionObject["data"]["sync"]["pollInterval"].Value<String>();
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

            // Audit retention.
            ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().AuditRetention = TimeSpan.Parse(optionObject["security"]["auditRetention"].Value<String>());

            if (optionObject["security"]["onlySubscribedAuth"].Value<Boolean>())
                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().OnlySubscribedFacilities = true;

            // Proxy
            if (optionObject["network"]["useProxy"].Value<Boolean>())
                ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>().ProxyAddress = optionObject["network"]["proxyAddress"].Value<String>();

            var optimize = optionObject["network"]["optimize"].Value<String>();
            OptimizationMethod method = OptimizationMethod.Gzip;
            if(!String.IsNullOrEmpty(optimize))
                switch(optimize)
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
                    case "off":
                        method = OptimizationMethod.None;
                        break;
                }

            foreach (var itm in ApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>().Client)
                if(itm.Binding.Optimize)
                    itm.Binding.OptimizationMethod = method;

            ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().AutoUpdateApplets = true;
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
            XamarinApplicationContext.Current.ConfigurationManager.Save();

            return new ConfigurationViewModel(XamarinApplicationContext.Current.Configuration);
        }

        /// <summary>
        /// Join a realm
        /// </summary>
        /// <param name="configData"></param>
        [RestOperation(UriPath = "/configuration/realm", Method = "POST", FaultProvider = nameof(ConfigurationFaultProvider))]
        [Demand(PolicyIdentifiers.AccessClientAdministrativeFunction)]
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

            if(configData.ContainsKey("client_secret"))
                ApplicationContext.Current.Application.ApplicationSecret = configData["client_secret"].Value<String>();

            // Set domain security
            switch(domainSecurity)
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

                new PolicyPermission(PermissionState.Unrestricted, PolicyIdentifiers.UnrestrictedAdministration).Demand();

                // We're allowed to access server admin!!!! Yay!!!
                // We're goin to conigure the realm settings now (all of them)
                var serviceClientSection = XamarinApplicationContext.Current.Configuration.GetSection<ServiceClientConfigurationSection>();
                if (serviceClientSection == null)
                {
                    serviceClientSection = new ServiceClientConfigurationSection()
                    {
                        RestClientType = typeof(RestClient)
                    };
                    XamarinApplicationContext.Current.Configuration.Sections.Add(serviceClientSection);
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
                    { ServiceEndpointType.ImmunizationIntegrationService, "hdsi" },
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
                            Timeout = itm.ServiceType == ServiceEndpointType.ImmunizationIntegrationService ? 60000 : 30000
                        }).ToList(),
                        Trace = enableTrace,
                        Name = serviceName
                    };

                    serviceClientSection.Client.Add(description);
                }

                ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new AmiPolicyInformationService());
                ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new HdsiPersistenceService());
                ApplicationContext.Current.GetService<IDataPersistenceService<Concept>>().Query(o => o.ConceptSets.Any(s=>s.Key == ConceptSetKeys.AddressComponentType));
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
                    var role = amiClient.GetRoles(o => o.Name == "SYNCHRONIZERS").CollectionItem.First();

                    // Does the user actually exist?
                    var existingClient = amiClient.GetUsers(o => o.UserName == deviceName);
                    if (existingClient.CollectionItem.Count > 0)
                    {
                        if (!replaceExisting)
                            throw new DuplicateNameException(Strings.err_duplicate_deviceName);
                        else
                            amiClient.UpdateUser(existingClient.CollectionItem.OfType<SecurityUserInfo>().First().Entity.Key.Value, new SanteDB.Core.Model.AMI.Auth.SecurityUserInfo()
                            {
                                PasswordOnly = true,
                                Entity = new SanteDB.Core.Model.Security.SecurityUser()
                                {
                                    Password = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret,
                                    UserName = deviceName
                                }
                            });
                    }
                    else
                        // Create user
                        amiClient.CreateUser(new SanteDB.Core.Model.AMI.Auth.SecurityUserInfo(new SanteDB.Core.Model.Security.SecurityUser()
                        {
                            CreationTime = DateTimeOffset.Now,
                            UserName = deviceName,
                            Key = Guid.NewGuid(),
                            UserClass = UserClassKeys.ApplicationUser,
                            SecurityHash = Guid.NewGuid().ToString(),
                            Password = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret
                        })
                        {
                            Roles = new List<string>() { "SYNCHORNIZERS" },
                        });

                    
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
                        }));

                        // TODO: Send device entity to server
                        //amiClient.CreateDeviceEntity(new DeviceEntity()
                        //{
                        //    SecurityDevice = newDevice.Entity,
                        //    StatusConceptKey = StatusKeys.Active,
                        //    ManufacturerModelName = Environment.MachineName,
                        //    OperatingSystemName = Environment.OSVersion.ToString(),
                        //});

                    }
                    else
                    {
                        amiClient.UpdateDevice(existingDevice.CollectionItem.OfType<SecurityDeviceInfo>().First().Entity.Key.Value, new SecurityDeviceInfo(new SanteDB.Core.Model.Security.SecurityDevice()
                        {

                            UpdatedTime = DateTime.Now,
                            Name = deviceName,
                            DeviceSecret = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret
                        }));
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
                    XamarinApplicationContext.Current.Configuration.Sections.Add(serviceClientSection);
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
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new HttpBasicIdentityProvider());
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(HttpBasicIdentityProvider).AssemblyQualifiedName);
                }
                else
                {
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Services.Add(new OAuthIdentityProvider());
                    ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(OAuthIdentityProvider).AssemblyQualifiedName);
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

                ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Domain = null;
                throw new UnauthorizedAccessException();
            }
            catch (Exception e)
            {
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
                return ApplicationContext.Current.GetService<IDataPersistenceService<TObject>>()?.Query(o=>o.SourceEntityKey == sourceKey) ?? new List<TObject>();
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
