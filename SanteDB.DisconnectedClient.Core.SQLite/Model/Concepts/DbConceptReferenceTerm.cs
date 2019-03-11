/*
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
 * User: justi
 * Date: 2019-1-12
 */
using SanteDB.Core.Data.QueryBuilder.Attributes;
using SQLite.Net.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Model.Concepts
{
    /// <summary>
    /// Concept reference term link
    /// </summary>
    [Table("concept_reference_term")]
    public class DbConceptReferenceTerm : DbIdentified
    {
        /// <summary>
        /// Gets or sets the concept UUID
        /// </summary>
        [Column("concept_uuid"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] ConceptUuid { get; set; }

        /// <summary>
        /// Gets or sets the target key
        /// </summary>
        [Column("reference_term_uuid"), ForeignKey(typeof(DbReferenceTerm), nameof(DbReferenceTerm.Uuid))]
        public byte[] ReferenceTermUuid { get; set; }

        /// <summary>
        /// Gets or sets the relationship type id
        /// </summary>
        [Column("rel_typ_id")]
        public byte[] RelationshipTypeUuid { get; set; }
    }
}
