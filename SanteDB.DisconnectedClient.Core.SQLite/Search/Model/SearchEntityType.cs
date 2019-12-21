﻿/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Search.Model
{
    /// <summary>
    /// Search entity master list of types
    /// </summary>
    [Table("entity")]
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
