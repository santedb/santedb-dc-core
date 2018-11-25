﻿/*
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
 * User: justin
 * Date: 2018-11-23
 */
using Newtonsoft.Json;
using SanteDB.DisconnectedClient.Ags.Configuration;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Xamarin;
using System;

namespace SanteDB.DisconnectedClient.Ags.Model
{
    /// <summary>
    /// Configuration view model
    /// </summary>
    [JsonObject("Configuration")]
    public class ConfigurationViewModel
    {
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
        public bool IsConfigured { get => (XamarinApplicationContext.Current as XamarinApplicationContext).ConfigurationManager.IsConfigured; }

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
            this.Ags = config.GetSection<AgsConfigurationSection>();
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
        /// <summary>
        /// Synchronization
        /// </summary>
        [JsonProperty("ags")]
        public AgsConfigurationSection Ags { get; set; }
    }


}