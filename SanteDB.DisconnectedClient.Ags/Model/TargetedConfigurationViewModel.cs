/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
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
