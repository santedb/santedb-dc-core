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
