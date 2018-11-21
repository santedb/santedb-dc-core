using Newtonsoft.Json;
using SanteDB.DisconnectedClient.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Model
{

    /// <summary>
    /// View model for provider
    /// </summary>
    [JsonObject]
    public class StorageProviderViewModel
    {
        /// <summary>
        /// The invariant name
        /// </summary>
        [JsonProperty("invariant")]
        public string Invariant { get; set; }

        /// <summary>
        /// The property name
        /// </summary>
        [JsonProperty("name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the options
        /// </summary>
        [JsonProperty("options")]
        public Dictionary<String, ConfigurationOptionType> Options { get; set; }
    }
}
