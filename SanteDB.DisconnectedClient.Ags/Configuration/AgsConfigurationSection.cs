using Newtonsoft.Json;
using SanteDB.DisconnectedClient.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Ags.Configuration
{
    /// <summary>
    /// Represents the configuration for the AGS
    /// </summary>
    [XmlType(nameof(AgsConfigurationSection), Namespace = "http://santedb.org/mobile/configuration")]
    [JsonObject]
    public class AgsConfigurationSection : IConfigurationSection
    {

        /// <summary>
        /// Construct the AGS configuration
        /// </summary>
        public AgsConfigurationSection()
        {
            this.Services = new List<AgsServiceConfiguration>();
        }

        /// <summary>
        /// Gets or sets the service configuration
        /// </summary>
        [XmlElement("service"), JsonProperty("service")]
        public List<AgsServiceConfiguration> Services { get; set; }
    }
}
