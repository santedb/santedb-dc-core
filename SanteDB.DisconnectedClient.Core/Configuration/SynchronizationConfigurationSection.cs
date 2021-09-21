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
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model;
using SanteDB.DisconnectedClient.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Configuration
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
            this.SubscribeTo = new List<string>();
            this.ForbiddenResouces = new List<SynchronizationForbidConfiguration>();
        }

        /// <summary>
        /// TRue it use big bundles (> 1000)
        /// </summary>
        [XmlAttribute("bigBundles")]
        [JsonProperty("bigBundles")]
        public bool BigBundles { get; set; }


        /// <summary>
        /// Resources which are forbidden from being sychronized
        /// </summary>
        [XmlArray("forbidSync")]
        [XmlArrayItem("add")]
        public List<SynchronizationForbidConfiguration> ForbiddenResouces { get; set; }

        /// <summary>
        /// Time between polling requests
        /// </summary>
        [XmlElement("pollInterval")]
        [JsonProperty("pollInterval")]
        public string PollIntervalXml
        {
            get => this.PollInterval.ToString();
            set
            {
                if (value == null)
                {
                    this.PollInterval = TimeSpan.MinValue;
                }
                else
                {
                    this.PollInterval = TimeSpan.Parse(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the mode of operation
        /// </summary>
        [XmlElement("mode")]
        [JsonProperty("mode")]
        public SynchronizationMode Mode { get; set; }

        /// <summary>
        /// Poll interval
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public TimeSpan PollInterval { get; set; }


        /// <summary>
        /// Subscription
        /// </summary>
        [XmlElement("subscribeType")]
        [JsonProperty("subscribeType")]
        public string SubscribeType { get; set; }

        /// <summary>
        /// When true never force a patch
        /// </summary>
        [XmlElement("safePatchOnly")]
        [JsonProperty("safePatch")]
        public bool SafePatchOnly { get; set; }

        /// <summary>
        /// Subscription
        /// </summary>
        [XmlArray("subscribeTo")]
        [XmlArrayItem("add")]
        [JsonProperty("subscribeTo")]
        public List<string> SubscribeTo { get; set; }


        /// <summary>
        /// Gets or sets the list of synchronization queries
        /// </summary>
        [XmlArray("resources")]
        [XmlArrayItem("add")]
        [JsonProperty("resources")]
        public List<SynchronizationResource> SynchronizationResources { get; set; }

        /// <summary>
        /// Use patches instead of full submission
        /// </summary>
        [XmlElement("allowPatch")]
        [JsonProperty("allowPatch")]
        public bool UsePatches { get; set; }
    }

    /// <summary>
    /// Resources which are forbidden from sync
    /// </summary>
    [XmlType(nameof(SynchronizationForbidConfiguration), Namespace = "http://santedb.org/mobile/configuration")]
    public class SynchronizationForbidConfiguration
    {
        /// <summary>
        /// Forbid ctor for serialization
        /// </summary>
        public SynchronizationForbidConfiguration()
        {

        }

        /// <summary>
        /// Forbid ctor
        /// </summary>
        /// <param name="op"></param>
        /// <param name="name"></param>
        public SynchronizationForbidConfiguration(SynchronizationOperationType op, string name)
        {
            this.Operations = op;
            this.ResourceName = name;
        }

        /// <summary>
        /// Forbidden operations
        /// </summary>
        [XmlAttribute("op")]
        public SynchronizationOperationType Operations { get; set; }

        /// <summary>
        /// Forbidden resource type
        /// </summary>
        [XmlText]
        public string ResourceName { get; set; }
    }

    /// <summary>
    /// Synchronization mode
    /// </summary>
    public enum SynchronizationMode
    {
        /// <summary>
        /// Synchronization mode - Cache results offline
        /// </summary>
        Sync = 0x1,
        /// <summary>
        /// Operate online only
        /// </summary>
        Online = 0x2,
        /// <summary>
        /// Operate offline only
        /// </summary>
        Offline = 0x4
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
        /// Always pull?
        /// </summary>
        [XmlAttribute("ignoreModifiedOn")]
        [JsonProperty("ignoreModifiedOn")]
        public bool Always { get; set; }

        /// <summary>
        /// One or more filters 
        /// </summary>
        [XmlArray("filters")]
        [XmlArrayItem("add")]
        [JsonProperty("filters")]
        public List<string> Filters { get; set; }

        /// <summary>
        /// The friendly name
        /// </summary>
        [XmlAttribute("name")]
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the resource type
        /// </summary>
        [XmlAttribute("resourceType")]
        [JsonProperty("resource")]
        public string ResourceAqn
        {
            get => this.ResourceType.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>().TypeName;
            set
            {
                this.ResourceType = typeof(IdentifiedData).GetTypeInfo().Assembly.ExportedTypes.FirstOrDefault(o => o.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>()?.TypeName == value);
            }
        }

        /// <summary>
        /// Gets the resource type
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public Type ResourceType
        {
            get; set;
        }

        /// <summary>
        /// Represents the triggers
        /// </summary>
        [XmlAttribute("trigger")]
        [JsonProperty("trigger")]
        public SynchronizationPullTriggerType Triggers { get; set; }
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