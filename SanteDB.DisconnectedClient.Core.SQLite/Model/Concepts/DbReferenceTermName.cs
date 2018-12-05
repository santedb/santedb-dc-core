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
    /// Reference term name
    /// </summary>
    [Table("reference_term_name")]
    public class DbReferenceTermName : DbIdentified
    {
        /// <summary>
        /// Gets or sets the ref term to which the nae applies
        /// </summary>
        [Column("reference_term_uuid"), ForeignKey(typeof(DbReferenceTerm), nameof(DbReferenceTerm.Uuid))]
        public byte[] ReferenceTermUuid { get; set; }


        /// <summary>
        /// Gets or sets the language code
        /// </summary>
        [Column("lang")]
        public String LanguageCode { get; set; }

        /// <summary>
        /// Gets orsets the value
        /// </summary>
        [Column("value")]
        public String Value { get; set; }
    }
}
