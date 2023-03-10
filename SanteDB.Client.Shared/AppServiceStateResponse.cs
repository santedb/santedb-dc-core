using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Client.Shared
{
    public class AppServiceStateResponse
    {
        [JsonProperty("version")]
        public string? Version { get; set; }
        [JsonProperty("online")]
        public bool Online { get; set; }
        [JsonProperty("hdsi")]
        public bool Hdsi { get; set; }
        [JsonProperty("ami")]
        public bool Ami { get; set; }
        [JsonProperty("client_id")]
        public string? ClientId { get; set; }
        [JsonProperty("device_id")]
        public string? DeviceId { get; set; }
        [JsonProperty("realm")]
        public string? Realm { get; set; }
        [JsonProperty("magic")]
        public string? Magic { get; set; }
    }
}
