﻿/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using Newtonsoft.Json;
using RestSrvr.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Ags.Configuration
{

    /// <summary>
    /// Represents configuration of a single AGS service
    /// </summary>
    [XmlType(nameof(AgsServiceConfiguration), Namespace = "http://santedb.org/mobile/configuration")]
    [JsonObject]
    public class AgsServiceConfiguration
    {

        /// <summary>
        /// AGS Service Configuration
        /// </summary>
        public AgsServiceConfiguration()
        {
            this.Behaviors = new List<AgsBehaviorConfiguration>();
            this.Endpoints = new List<AgsEndpointConfiguration>();
        }

        /// <summary>
        /// Creates a service configuration from the specified type
        /// </summary>
        public AgsServiceConfiguration(Type type) : this()
        {
            this.Name = type.GetCustomAttribute<ServiceBehaviorAttribute>()?.Name ?? type.FullName;
            this.ServiceType = type;
        }

        /// <summary>
        /// Gets or sets the name of the service
        /// </summary>
        [XmlAttribute("name"), JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the behavior
        /// </summary>
        [XmlAttribute("serviceBehavior"), JsonProperty("serviceBehavior")]
        public String ServiceTypeXml { get; set; }

        /// <summary>
        /// Service ignore
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public Type ServiceType { get => Type.GetType(this.ServiceTypeXml); set => this.ServiceTypeXml = value.AssemblyQualifiedName; }

        /// <summary>
        /// Gets or sets the behavior of the AGS endpoint
        /// </summary>
        [XmlArray("behavior"), XmlArrayItem("add"), JsonProperty("behavior")]
        public List<AgsBehaviorConfiguration> Behaviors { get; set; }

        /// <summary>
        /// Gets or sets the endpoints 
        /// </summary>
        [XmlElement("endpoint"), JsonProperty("endpoint")]
        public List<AgsEndpointConfiguration> Endpoints { get; set; }

    }
}