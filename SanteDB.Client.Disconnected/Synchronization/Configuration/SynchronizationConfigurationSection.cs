/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace SanteDB.Client.Disconnected.Data.Synchronization.Configuration
{
    /// <summary>
    /// Represents a configuration section which controls the synchronization of resources with an upstream
    /// </summary>
    [XmlType(nameof(SynchronizationConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class SynchronizationConfigurationSection : IConfigurationSection
    {

        /// <summary>
        /// True it use big bundles (> 1000)
        /// </summary>
        [XmlAttribute("bigBundles"), JsonProperty("bigBundles")]
        public bool BigBundles { get; set; }

        /// <summary>
        /// True if automatic merging should occur on the server and client
        /// </summary>
        [XmlAttribute("overwriteServer"), JsonProperty("overwriteServer")]
        public bool OverwriteServer { get; set; }

        /// <summary>
        /// Use patches instead of updates
        /// </summary>
        [XmlAttribute("usePatches"), JsonProperty("usePatches")]
        public bool UsePatches { get; set; }

        /// <summary>
        /// Gets the mode of sync
        /// </summary>
        [XmlAttribute("mode"), JsonProperty("mode")]
        public SynchronizationMode Mode { get; set; }

        /// <summary>
        /// Resources which are forbidden from being sychronized
        /// </summary>
        [XmlArray("forbidSending")]
        [XmlArrayItem("add")]
        [JsonProperty("forbidSending")]
        public List<ResourceTypeReferenceConfiguration> ForbidSending { get; set; }

        /// <summary>
        /// Gets or sets the subscriptions which are active for this configuration
        /// </summary>
        [XmlArray("subscriptions"), XmlArrayItem("add"), JsonProperty("subscriptions")]
        public List<Guid> Subscriptions { get; set; }

        /// <summary>
        /// The type of resource that this is binding to
        /// </summary>
        [XmlElement("subscribeTo"), JsonProperty("subscribeTo")]
        public ResourceTypeReferenceConfiguration SubscribeToResource { get; set; }

        /// <summary>
        /// Gets or sets the subscribed objects
        /// </summary>
        [XmlArray("subscribed"), XmlArrayItem("add"), JsonProperty("subscribed")]
        public List<Guid> SubscribedObjects { get; set; }

        /// <summary>
        /// The time between polling requests
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public TimeSpan PollInterval { get; set; }

        /// <summary>
        /// Time between polling requests
        /// </summary>
        [XmlElement("pollInterval"), JsonProperty("pollInterval")]
        public string PollIntervalXml
        {
            get => XmlConvert.ToString(PollInterval);
            set
            {
                if (value == null)
                {
                    PollInterval = TimeSpan.MaxValue;
                }
                else
                {
                    PollInterval = XmlConvert.ToTimeSpan(value);
                }
            }
        }

    }
}
