/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you
 * may not use this file except in compliance with the License. You may
 * obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 *
 * User: fyfej
 * Date: 2023-3-10
 */
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
        [FormElement("challenge")]
        public String Challenge { get; set; }
        [FormElement("response")]
        public String Response { get; set; }
        [FormElement("scope")]
        public string Scope { get; internal set; }
    }
}
