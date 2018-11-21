using Newtonsoft.Json;
using System;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Ags.Configuration
{
    /// <summary>
    /// Represents a single behavior configuration element
    /// </summary>
    [XmlType(nameof(AgsBehaviorConfiguration), Namespace = "http://santedb.org/mobile/configuraton")]
    [JsonObject]
    public class AgsBehaviorConfiguration
    {

        public AgsBehaviorConfiguration()
        {

        }

        /// <summary>
        /// AGS Behavior Configuration
        /// </summary>
        public AgsBehaviorConfiguration(Type behaviorType)
        {
            this.Type = behaviorType;
        }

        /// <summary>
        /// Gets or sets the name
        /// </summary>
        [XmlAttribute("type"), JsonProperty("type")]
        public string XmlType { get; set; }

        /// <summary>
        /// Gets the type of the binding
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public Type Type
        {
            get
            {
                return Type.GetType(this.XmlType);
            }
            set
            {
                this.XmlType = value.AssemblyQualifiedName;
            }
        }

        /// <summary>
        /// Gets or sets the special configuration for the binding
        /// </summary>
        [XmlElement("configuration"), JsonProperty("configuration")]
        public XElement Configuration { get; set; } 
    }
}