using SanteDB.Core.Data.QueryBuilder.Attributes;
using SQLite.Net.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Security.Audit.Model
{
    /// <summary>
    /// Audit metadata
    /// </summary>
    [Table("audit_metadata")]
    public class DbAuditMetadata
    {
        /// <summary>
        /// Identifier of the object
        /// </summary>
        [Column("id"), PrimaryKey]
        public byte[] Id { get; set; }

        /// <summary>
        /// Gets or sets the audit identifier
        /// </summary>
        [Column("audit_id"), ForeignKey(typeof(DbAuditData), nameof(DbAuditData.Id))]
        public byte[] AuditId { get; set; }

        /// <summary>
        /// Metadata key for audits
        /// </summary>
        [Column("attr")]
        public int MetadataKey { get; set; }

        /// <summary>
        /// The value of the audit metadata
        /// </summary>
        [Column("val")]
        public string Value { get; set; }
    }
}
