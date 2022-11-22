using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.OAuth
{
    public class OAuthClientTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        [JsonProperty("id_token")]
        public string IdToken { get; set; }
        [JsonProperty("token_type")]
        public string TokenType { get; set; }
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }
        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        // HACK: Allows for conveying upstream errors
        [JsonProperty("error")]
        public string Error { get; set; }
        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }
    }
}
