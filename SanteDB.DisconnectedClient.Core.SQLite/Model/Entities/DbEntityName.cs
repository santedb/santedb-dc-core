/*
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
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Entities
{
    /// <summary>
    /// Represents an entity name related to an entity
    /// </summary>
    [Table("entity_name")]
    public class DbEntityName : DbEntityLink
    {

        /// <summary>
        /// Gets or sets the use concept.
        /// </summary>
        /// <value>The use concept.</value>
        [Column("use"), MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] UseConceptUuid
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Represents a component of a name
    /// </summary>
    [Table("entity_name_comp")]
    public class DbEntityNameComponent : DbGenericNameComponent
    {

        /// <summary>
        /// Gets or sets the name identifier.
        /// </summary>
        /// <value>The name identifier.</value>
        [Column("name_uuid"), MaxLength(16), NotNull, Indexed, ForeignKey(typeof(DbEntityName), nameof(DbEntityName.Uuid))]
        public byte[] NameUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the value identifier of the name value
        /// </summary>
        [Column("value_id"), MaxLength(16), NotNull, ForeignKey(typeof(DbPhoneticValue), nameof(DbPhoneticValue.Uuid)), AlwaysJoin]
        public override byte[] ValueUuid
        {
            get; set;
        }

        /// <summary>
        /// Sequence identifier
        /// </summary>
        [Column("sequence")]
        public int Sequence { get; set; }

        /// <summary>
        /// Query result
        /// </summary>
        public class QueryResult : DbEntityNameComponent
        {

            /// <summary>
            /// Gets or sets the value of the address component
            /// </summary>
            [Column("value")]
            public String Value { get; set; }

        }
    }

    /// <summary>
    /// Phonetic value table
    /// </summary>
    [Table("phonetic_value")]
    public class DbPhoneticValue : DbIdentified
    {

        /// <summary>
        /// Value of the phonetic table
        /// </summary>
        [Column("value"), NotNull]
        public String Value { get; set; }
    }

}

