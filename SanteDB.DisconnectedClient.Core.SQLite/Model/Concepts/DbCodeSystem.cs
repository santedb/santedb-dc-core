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
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Concepts
{
    /// <summary>
    /// Represents a code system 
    /// </summary>
    [Table("code_system")]
    public class DbCodeSystem : DbBaseData
    {
        /// <summary>
        /// Gets or sets the name of the code system
        /// </summary>
        [Column("name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the oid
        /// </summary>
        [Column("oid")]
        public String Oid { get; set; }

        /// <summary>
        /// Gets or sets the domain CX.4
        /// </summary>
        [Column("domain")]
        public String Domain { get; set; }

        /// <summary>
        /// Gets or sets the url
        /// </summary>
        [Column("url")]
        public String Url { get; set; }

        /// <summary>
        /// Gets or sets the version text from the CS authorty
        /// </summary>
        [Column("version")]
        public String VersionText { get; set; }

        /// <summary>
        /// Gets or sets the description
        /// </summary>
        [Column("descr")]
        public String Description { get; set; }
    }
}