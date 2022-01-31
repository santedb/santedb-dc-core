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
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Search.Model
{
    /// <summary>
    /// Search entity master list of types
    /// </summary>
    [Table("search_entity")]
    public class SearchEntityType
    {

        /// <summary>
        /// Search entity type ctor
        /// </summary>
        public SearchEntityType()
        {
            this.Key = Guid.NewGuid().ToByteArray();
        }

        /// <summary>
        /// Gets or sets the key
        /// </summary>
        [Column("key"), PrimaryKey, MaxLength(16)]
        public byte[] Key { get; set; }

        /// <summary>
        /// Gets or sets the version in the index
        /// </summary>
        [Column("version"), MaxLength(16), NotNull]
        public byte[] VersionKey { get; set; }

        /// <summary>
        /// Represents the type of model
        /// </summary>
        [Column("type"), NotNull, Indexed]
        public String SearchType { get; set; }


    }
}
