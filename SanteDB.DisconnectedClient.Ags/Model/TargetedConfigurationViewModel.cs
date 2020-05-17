using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Ags.Model
{
    /// <summary>
    /// Represents control to go to a targeted configuration
    /// </summary>
    [JsonObject(nameof(TargetedConfigurationViewModel))]
    public class TargetedConfigurationViewModel
    {

        /// <summary>
        /// Remote URI to be pushed
        /// </summary>
        [JsonProperty("target")]
        public String RemoteUri { get; set; }

        /// <summary>
        /// The user to authenticate as
        /// </summary>
        [JsonProperty("user")]
        public String UserName { get; set; }

        /// <summary>
        /// The password to authenticate as
        /// </summary>
        [JsonProperty("password")]
        public String Password { get; set; }

        /// <summary>
        /// Parameters for the object
        /// </summary>
        [JsonProperty("parms")]
        public Dictionary<String, Object> Parameters { get; set; }
    }
}
