﻿/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-11-23
 */
using Newtonsoft.Json;
using SanteDB.DisconnectedClient.Core.Data;
using System;
using System.Collections.Generic;

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