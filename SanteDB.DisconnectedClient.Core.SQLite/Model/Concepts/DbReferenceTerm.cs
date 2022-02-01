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
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Concepts
{
    /// <summary>
    /// Reference term table
    /// </summary>
    [Table("reference_term")]
    public class DbReferenceTerm : DbBaseData
    {
        /// <summary>
        /// Gets or sets the code syste
        /// </summary>
        [Column("cs_id"), ForeignKey(typeof(DbCodeSystem), nameof(DbCodeSystem.Uuid))]
        public byte[] CodeSystemUuid { get; set; }

        /// <summary>
        /// Gets or sets the mnemonic
        /// </summary>
        [Column("mnemonic")]
        public String Mnemonic { get; set; }
    }
}
