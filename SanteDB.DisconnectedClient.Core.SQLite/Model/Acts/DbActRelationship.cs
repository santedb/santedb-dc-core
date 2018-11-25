﻿/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-6-28
 */
using SanteDB.Core.Data.QueryBuilder.Attributes;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SQLite.Net.Attributes;

namespace SanteDB.DisconnectedClient.SQLite.Model.Acts
{
    /// <summary>
    /// Identifies relationships between acts
    /// </summary>
    [Table("act_relationship")]
    public class DbActRelationship : DbIdentified
    {

        /// <summary>
        /// Gets or sets the source act of the relationship
        /// </summary>
        [Column("act_uuid"), MaxLength(16), NotNull, Indexed, ForeignKey(typeof(DbAct), nameof(DbAct.Uuid))]
        public byte[] SourceUuid { get; set; }

        /// <summary>
        /// Gets or sets the target entity
        /// </summary>
        [Column("target"), MaxLength(16), ForeignKey(typeof(DbAct), nameof(DbAct.Uuid))]
        public byte[] TargetUuid { get; set; }

        /// <summary>
        /// Gets or sets the link type concept
        /// </summary>
        [Column("relationshipType"), MaxLength(16), NotNull, ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] RelationshipTypeUuid { get; set; }

    }
}
