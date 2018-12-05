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
    /// Reference term table
    /// </summary>
    [Table("reference_term")]
    public class DbReferenceTerm : DbBaseData
    {
        /// <summary>
        /// Gets or sets the code syste
        /// </summary>
        [Column("cs_id"), ForeignKey(typeof(DbCodeSystem), nameof(DbCodeSystem.Uuid))]
        public byte[] CodeSystemUuid { get; set; }

        /// <summary>
        /// Gets or sets the mnemonic
        /// </summary>
        [Column("mnemonic")]
        public String Mnemonic { get; set; }
    }
}
