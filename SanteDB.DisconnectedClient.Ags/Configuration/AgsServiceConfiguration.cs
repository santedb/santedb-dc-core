using Newtonsoft.Json;
using RestSrvr.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Ags.Configuration
{

    /// <summary>
    /// Represents configuration of a single AGS service
    /// </summary>
    [XmlType(nameof(AgsServiceConfiguration), Namespace = "http://santedb.org/mobile/configuration")]
    [JsonObject]
    public class AgsServiceConfiguration
    {

        /// <summary>
        /// AGS Service Configuration
        /// </summary>
        public AgsServiceConfiguration()
        {
            this.Behaviors = new List<AgsBehaviorConfiguration>();
            this.Endpoints = new List<AgsEndpointConfiguration>();
        }

        /// <summary>
        /// Creates a service configuration from the specified type
        /// </summary>
        internal AgsServiceConfiguration(Type type) : this()
        {
            this.Name = type.GetCustomAttribute<ServiceBehaviorAttribute>()?.Name ?? type.FullName;
            this.ServiceType = type;
        }

        /// <summary>
        /// Gets or sets the name of the service
        /// </summary>
        [XmlAttribute("name"), JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the behavior
        /// </summary>
        [XmlAttribute("serviceBehavior"), JsonProperty("serviceBehavior")]
        public String ServiceTypeXml { get; set; }

        /// <summary>
        /// Service ignore
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public Type ServiceType { get => Type.GetType(this.ServiceTypeXml); set => this.ServiceTypeXml = value.AssemblyQualifiedName; }

        /// <summary>
        /// Gets or sets the behavior of the AGS endpoint
        /// </summary>
        [XmlArray("behavior"), XmlArrayItem("add"), JsonProperty("behavior")]
        public List<AgsBehaviorConfiguration> Behaviors { get; set; }

        /// <summary>
        /// Gets or sets the endpoints 
        /// </summary>
        [XmlElement("endpoint"), JsonProperty("endpoint")]
        public List<AgsEndpointConfiguration> Endpoints { get; set; }

    }
}