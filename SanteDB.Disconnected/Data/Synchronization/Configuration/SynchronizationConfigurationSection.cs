using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SanteDB.Disconnected.Data.Synchronization.Configuration
{
    /// <summary>
    /// Represents a configuration section which controls the synchronization of resources with an upstream
    /// </summary>
    [XmlType(nameof(SynchronizationConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class SynchronizationConfigurationSection : IConfigurationSection
    {

        /// <summary>
        /// TRue it use big bundles (> 1000)
        /// </summary>
        [XmlAttribute("bigBundles"), JsonProperty("bigBundles")]
        public bool BigBundles { get; set; }


        /// <summary>
        /// Resources which are forbidden from being sychronized
        /// </summary>
        [XmlArray("forbidResources")]
        [XmlArrayItem("add")]
        [JsonProperty("forbiddenResources")]
        public List<ResourceTypeReferenceConfiguration> ForbiddenResouces { get; set; }

        /// <summary>
        /// Gets or sets the subscriptions which are active for this configuration
        /// </summary>
        [XmlArray("subscriptions"), XmlArrayItem("add"), JsonProperty("subscriptions")]
        public List<SynchronizationResourceConfiguration> Subscriptions { get; set; }

        /// <summary>
        /// Gets or sets the subscribed objects
        /// </summary>
        [XmlArray("subscribed"), XmlArrayItem("add"), JsonProperty("subscribed")]
        public List<SubscribedObjectConfiguration> SubscribedObjects { get; set; }

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
