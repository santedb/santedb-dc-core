/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * User: trevor
 * Date: 2023-4-19
 */
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Client.Shared
{
    /// <summary>
    /// A serialization class for the response of app state for consumption by the web runtime.
    /// </summary>
    public class AppServiceStateResponse
    {
        /// <summary>
        /// The app version
        /// </summary>
        [JsonProperty("version")]
        public string? Version { get; set; }
        /// <summary>
        /// True when the runtime can access an upstream realm.
        /// </summary>
        [JsonProperty("online")]
        public bool Online { get; set; }
        /// <summary>
        /// True when the upstream HDSI service is available.
        /// </summary>
        [JsonProperty("hdsi")]
        public bool Hdsi { get; set; }
        /// <summary>
        /// True when the upstream AMI service is available.
        /// </summary>
        [JsonProperty("ami")]
        public bool Ami { get; set; }
        /// <summary>
        /// The app client id for security operations.
        /// </summary>
        [JsonProperty("client_id")]
        public string? ClientId { get; set; }
        /// <summary>
        /// The device id for security operations.
        /// </summary>
        [JsonProperty("device_id")]
        public string? DeviceId { get; set; }
        /// <summary>
        /// When part of an upstream realm, the realm that this app is part of.
        /// </summary>
        [JsonProperty("realm")]
        public string? Realm { get; set; }
        /// <summary>
        /// The unique random identifier to provide a security safeguard against unauthorized use of the app.
        /// </summary>
        [JsonProperty("magic")]
        public string? Magic { get; set; }
    }
}
