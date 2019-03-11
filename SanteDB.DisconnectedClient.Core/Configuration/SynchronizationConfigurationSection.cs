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
using SanteDB.Core.Configuration;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Core.Configuration
{
    /// <summary>
    /// Configuration related to synchronization
    /// </summary>
	[XmlType(nameof(SynchronizationConfigurationSection), Namespace = "http://santedb.org/mobile/configuration")]
    public class SynchronizationConfigurationSection : IConfigurationSection
    {
        /// <summary>
        /// Synchronization configuration section ctor
        /// </summary>
        public SynchronizationConfigurationSection()
        {
            this.SynchronizationResources = new List<SynchronizationResource>();
            this.Facilities = new List<string>();
        }

        /// <summary>
        /// Time between polling requests
        /// </summary>
        [XmlElement("pollInterval"), JsonProperty("pollInterval")]
        public String PollIntervalXml
        {
            get
            {
                return this.PollInterval.ToString();
            }
            set
            {
                if (value == null)
                    this.PollInterval = TimeSpan.MinValue;
                else
                    this.PollInterval = TimeSpan.Parse(value);
            }
        }

        /// <summary>
        /// Gets or sets the mode of operation
        /// </summary>
        [XmlElement("mode"), JsonProperty("mode")]
        public SynchronizationMode Mode { get; set; }

        /// <summary>
        /// Poll interval
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public TimeSpan PollInterval { get; set; }

        /// <summary>
        /// Gets or sets the list of synchronization queries
        /// </summary>
        [XmlElement("resource"), JsonProperty("resource")]
        public List<SynchronizationResource> SynchronizationResources { get; set; }

        /// <summary>
        /// Subscription
        /// </summary>
        [XmlElement("subscribe"), JsonProperty("subscribe")]
        public List<String> Facilities { get; set; }

        /// <summary>
        /// When true never force a patch
        /// </summary>
        [XmlElement("safePatchOnly"), JsonProperty("safePatch")]
        public bool SafePatchOnly { get; set; }
    }

    /// <summary>
    /// Synchronization mode
    /// </summary>
    public enum SynchronizationMode
    {
        Sync,
        Online,
        Offline
    }

    /// <summary>
    /// Synchronization 
    /// </summary>
    [XmlType(nameof(SynchronizationResource), Namespace = "http://santedb.org/mobile/configuration")]
    public class SynchronizationResource
    {
        /// <summary>
        /// default ctor
        /// </summary>
        public SynchronizationResource()
        {
            this.Filters = new List<string>();
        }

        /// <summary>
        /// Gets the resource type
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public Type ResourceType
        {
            get; set;
        }

        /// <summary>
        /// Represents the triggers
        /// </summary>
        [XmlAttribute("trigger"), JsonProperty("trigger")]
        public SynchronizationPullTriggerType Triggers { get; set; }

        /// <summary>
        /// Gets or sets the resource type
        /// </summary>
        [XmlAttribute("resourceType"), JsonProperty("resource")]
        public string ResourceAqn
        {
            get
            {
                return this.ResourceType.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>().TypeName;
            }
            set
            {
                this.ResourceType = typeof(IdentifiedData).GetTypeInfo().Assembly.ExportedTypes.FirstOrDefault(o => o.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>()?.TypeName == value);
            }
        }

        /// <summary>
        /// One or more filters 
        /// </summary>
        [XmlElement("filter"), JsonProperty("filter")]
        public List<string> Filters { get; set; }

        /// <summary>
        /// Always pull?
        /// </summary>
        [XmlAttribute("ignoreModifiedOn"), JsonProperty("ignoreModifiedOn")]
        public bool Always { get; set; }

        /// <summary>
        /// The friendly name
        /// </summary>
        [XmlAttribute("name"), JsonProperty("name")]
        public String Name { get; set; }

    }


    /// <summary>
    /// Represents synchronization pull triggers
    /// </summary>
    [XmlType(nameof(SynchronizationPullTriggerType), Namespace = "http://santedb.org/mobile/configuration")]
    [Flags]
    public enum SynchronizationPullTriggerType
    {
        Never = 0x0,
        Always = OnStart | OnCommit | OnStop | OnPush | OnNetworkChange | PeriodicPoll,
        OnStart = 0x01,
        OnCommit = 0x02,
        OnStop = 0x04,
        OnPush = 0x08,
        OnNetworkChange = 0x10,
        PeriodicPoll = 0x20,
        Manual = 0x40
    }
}