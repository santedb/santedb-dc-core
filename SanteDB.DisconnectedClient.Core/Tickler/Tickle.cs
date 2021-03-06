﻿/*
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
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Tickler
{
    /// <summary>
    /// Represents a tickle message
    /// </summary>
    [JsonObject(nameof(Tickle)), XmlType(nameof(Tickle), Namespace = "http://santedb.org/mobile/tickle")]
    public class Tickle
    {

        /// <summary>
        /// Creates a an empty tickle
        /// </summary>
        public Tickle()
        {
            this.Id = Guid.NewGuid();
            this.Created = DateTime.Now;
        }

        /// <summary>
        /// Creates a new tickle
        /// </summary>
        public Tickle(Guid to, TickleType type, String text, DateTime? expiry = null) : this()
        {
            this.Target = to;
            this.Type = type;
            this.Text = text;
            this.Expiry = expiry ?? DateTime.MaxValue;
        }

        /// <summary>
        /// Identifier of the tickle
        /// </summary>
        [JsonProperty("id"), XmlAttribute("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the text
        /// </summary>
        [JsonProperty("text"), XmlText]
        public String Text { get; set; }

        /// <summary>
        /// Gets the type of tickle
        /// </summary>
        [JsonProperty("type"), XmlAttribute("type")]
        public TickleType Type { get; set; }

        /// <summary>
        /// Gets or sets the expiration of the tickle
        /// </summary>
        [JsonProperty("exp"), XmlAttribute("exp")]
        public DateTime Expiry { get; set; }

        /// <summary>
        /// The time the tickle was created
        /// </summary>
        [JsonProperty("creationTime"), XmlAttribute("creationTime")]
        public DateTime Created { get; set; }

        /// <summary>
        /// The target of the tickle
        /// </summary>
        [JsonProperty("target"), XmlAttribute("to")]
        public Guid Target { get; set; }

    }
}
