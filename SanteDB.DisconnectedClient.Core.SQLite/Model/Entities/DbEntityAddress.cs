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
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Entities
{
    /// <summary>
    /// Represents one or more entity addresses linked to an Entity
    /// </summary>
    [Table("entity_address")]
    public class DbEntityAddress : DbEntityLink
    {

        /// <summary>
        /// Gets or sets the use concept identifier.
        /// </summary>
        /// <value>The use concept identifier.</value>
        [Column("use"), MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] UseConceptUuid
        {
            get;
            set;
        }

    }

    /// <summary>
    /// Represents an identified address component
    /// </summary>
    [Table("entity_address_comp")]
    public class DbEntityAddressComponent : DbGenericNameComponent
    {

        /// <summary>
        /// Gets or sets the address identifier.
        /// </summary>
        /// <value>The address identifier.</value>
        [Column("address_uuid"), MaxLength(16), Indexed, ForeignKey(typeof(DbEntityAddress), nameof(DbEntityAddress.Uuid))]
        public byte[] AddressUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the value identifier of the name value
        /// </summary>
        [Column("value_id"), MaxLength(16), NotNull, ForeignKey(typeof(DbAddressValue), nameof(DbAddressValue.Uuid)), AlwaysJoin]
        public override byte[] ValueUuid
        {
            get; set;
        }

        /// <summary>
        /// Query result
        /// </summary>
        public class QueryResult : DbEntityAddressComponent
        {

            /// <summary>
            /// Gets or sets the value of the address component
            /// </summary>
            [Column("value")]
            public String Value { get; set; }

        }

    }


    /// <summary>
    /// Represents unique values
    /// </summary>
    [Table("entity_addr_val")]
    public class DbAddressValue : DbIdentified
    {

        /// <summary>
        /// Gets or sets the value of the address
        /// </summary>
        [Column("value"), NotNull]
        public String Value { get; set; }
    }
}

