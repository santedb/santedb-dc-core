using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// Client configuration section
    /// </summary>
    [XmlType(nameof(ClientConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class ClientConfigurationSection : IConfigurationSection
    {

        /// <summary>
        /// Automatically update applets
        /// </summary>
        [XmlAttribute("autoUpdateApplets"), JsonProperty("autoUpdateApplets")]
        public bool AutoUpdateApplets { get; set; }

        /// <summary>
        /// User interface solution
        /// </summary>
        [XmlAttribute("solution"), JsonProperty("solution")]
        public string UiSolution { get; set; }
    }
}
