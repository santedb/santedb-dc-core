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

namespace SanteDB.DisconnectedClient.SQLite.Model.Extensibility
{
    /// <summary>
    /// Represents a database template definition
    /// </summary>
    [Table("template")]
    public class DbTemplateDefinition : DbBaseData
    {
        /// <summary>
        /// Gets the OID of the template
        /// </summary>
        [Column("oid")]
        public String Oid { get; set; }

        /// <summary>
        /// Gets the name of the template
        /// </summary>
        [Column("name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets the mnemonic
        /// </summary>
        [Column("mnemonic"), Collation("NOCASE"), Unique]
        public String Mnemonic { get; set; }

    }
}
