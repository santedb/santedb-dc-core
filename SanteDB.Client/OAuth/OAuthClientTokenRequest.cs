using SanteDB.Core.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.OAuth
{
    public class OAuthClientTokenRequest
    {
        [FormElement("grant_type")]
        public string GrantType { get; set; }
        [FormElement("username")]
        public string Username { get; set; }
        [FormElement("password")]
        public string Password { get; set; }
        [FormElement("client_id")]
        public string ClientId { get; set; }
        [FormElement("client_secret")]
        public string ClientSecret { get; set; }
        [FormElement("nonce")]
        public string Nonce { get; set; }
        [FormElement("refresh_token")]
        public string RefreshToken { get; set; }
        [FormElement("x_mfa")]
        public string MfaCode { get; set; }
    }
}
