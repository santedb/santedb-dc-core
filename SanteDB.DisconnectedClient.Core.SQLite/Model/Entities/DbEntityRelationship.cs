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

namespace SanteDB.DisconnectedClient.SQLite.Model.Entities
{
    /// <summary>
    /// Represents a relationship between two entities
    /// </summary>
    [Table("entity_relationship")]
    public class DbEntityRelationship : DbIdentified
    {

        /// <summary>
        /// Gets or sets the entity identifier.
        /// </summary>
        /// <value>The entity identifier.</value>
        [Column("entity_uuid"), MaxLength(16), NotNull, Indexed, ForeignKey(typeof(DbEntity), nameof(DbEntity.Uuid))]
        public byte[] SourceUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the target entity
        /// </summary>
        [Column("target"), MaxLength(16), NotNull, ForeignKey(typeof(DbEntity), nameof(DbEntity.Uuid))]
        public byte[] TargetUuid { get; set; }


        /// <summary>
        /// Gets or sets the link type concept
        /// </summary>
        [Column("relationshipType"), MaxLength(16), NotNull, ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] RelationshipTypeUuid { get; set; }

        /// <summary>
        /// Quantity 
        /// </summary>
        [Column("quantity")]
        public int? Quantity { get; set; }
    }
}
