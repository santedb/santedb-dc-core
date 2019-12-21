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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Configuration;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using SanteDB.DisconnectedClient.Xamarin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Ags.Model
{
    /// <summary>
    /// Configuration view model
    /// </summary>
    [JsonObject("Configuration")]
    public class ConfigurationViewModel
    {
        /// <summary>
        /// Configuration view model
        /// </summary>
        public ConfigurationViewModel()
        {

        }

        /// <summary>
        /// Get the type
        /// </summary>
        [JsonProperty("$type")]
        public String Type { get { return "Configuration"; } }

        /// <summary>
        /// Return true if configured
        /// </summary>
        [JsonProperty("isConfigured")]
        public bool IsConfigured { get => XamarinApplicationContext.Current.ConfigurationPersister.IsConfigured; }

        /// <summary>
        /// Configuation
        /// </summary>
        /// <param name="config"></param>
        public ConfigurationViewModel(SanteDBConfiguration config)
        {
            if (config == null) return;
            this.RealmName = config.GetSection<SecurityConfigurationSection>()?.Domain;
            this.Security = config.GetSection<SecurityConfigurationSection>();

            if (ApplicationContext.Current.Configuration.SectionTypes.Any(o => o.Type == typeof(DcDataConfigurationSection)))
                this.Data = config.GetSection<DcDataConfigurationSection>();
            this.Applet = config.GetSection<AppletConfigurationSection>();
            this.Application = config.GetSection<ApplicationServiceContextConfigurationSection>();
            this.Log = config.GetSection<DiagnosticsConfigurationSection>();
            this.Network = config.GetSection<ServiceClientConfigurationSection>();

            try
            {
                this.Synchronization = config.GetSection<SynchronizationConfigurationSection>();
            }
            catch { }
            this.Ags = config.GetSection<AgsConfigurationSection>();

            this.OtherSections = config.Sections.Where(o => !typeof(ConfigurationViewModel).GetRuntimeProperties().Any(p => p.PropertyType.IsAssignableFrom(o.GetType()))).ToList();
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
        public DcDataConfigurationSection Data { get; set; }
        /// <summary>
        /// Gets or sets applet
        /// </summary>
        [JsonProperty("applet")]
        public AppletConfigurationSection Applet { get; set; }
        /// <summary>
        /// Gets or sets application
        /// </summary>
        [JsonProperty("application")]
        public ApplicationServiceContextConfigurationSection Application { get; set; }
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
        /// <summary>
        /// Synchronization
        /// </summary>
        [JsonProperty("ags")]
        public AgsConfigurationSection Ags { get; set; }

        /// <summary>
        /// Represents other sections
        /// </summary>
        [JsonProperty("others")]
        public List<object> OtherSections { get; set; }
    }


}
