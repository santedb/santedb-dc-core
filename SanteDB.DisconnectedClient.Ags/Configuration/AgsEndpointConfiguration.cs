using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Ags.Configuration
{
    /// <summary>
    /// Represents an endpoint configuration
    /// </summary>
    [XmlType(nameof(AgsEndpointConfiguration), Namespace = "http://santedb.org/mobile/configuration")]
    [JsonObject]
    public class AgsEndpointConfiguration
    {

        /// <summary>
        /// AGS Endpoint CTOR
        /// </summary>
        public AgsEndpointConfiguration()
        {
            this.Behaviors = new List<AgsBehaviorConfiguration>();
        }

        /// <summary>
        /// Gets or sets the contract type
        /// </summary>
        [XmlAttribute("contract"), JsonProperty("contract")]
        public String ContractXml { get; set; }

        /// <summary>
        /// Gets or sets the Contract type
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public Type Contract {
            get => Type.GetType(this.ContractXml);
            set => this.ContractXml = value.AssemblyQualifiedName;
        }

        /// <summary>
        /// Gets or sets the address
        /// </summary>
        [XmlAttribute("address"), JsonProperty("address")]
        public String Address { get; set; }

        /// <summary>
        /// Gets the bindings 
        /// </summary>
        [XmlArray("behavior"), XmlArrayItem("add"), JsonProperty("behavior")]
        public List<AgsBehaviorConfiguration> Behaviors { get; set; }

    }
}