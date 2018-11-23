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
 * Date: 2018-6-28
 */
using Newtonsoft.Json;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Diagnostics;
using SanteDB.Core.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Xamarin.Services.Model
{
    /// <summary>
    /// Represents a bug report
    /// </summary>
    [JsonObject("BugReport")]
    public class BugReport : DiagnosticReport
    {

        /// <summary>
        /// Include diagnostics data
        /// </summary>
        [JsonProperty("_includeData")]
        public bool IncludeData { get; set; }

    }
}
