using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Concepts
{
    /// <summary>
    /// Represents a code system 
    /// </summary>
    [Table("code_system")]
    public class DbCodeSystem : DbBaseData
    {
        /// <summary>
        /// Gets or sets the name of the code system
        /// </summary>
        [Column("name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the oid
        /// </summary>
        [Column("oid")]
        public String Oid { get; set; }

        /// <summary>
        /// Gets or sets the domain CX.4
        /// </summary>
        [Column("domain")]
        public String Domain { get; set; }

        /// <summary>
        /// Gets or sets the url
        /// </summary>
        [Column("url")]
        public String Url { get; set; }

        /// <summary>
        /// Gets or sets the version text from the CS authorty
        /// </summary>
        [Column("version")]
        public String VersionText { get; set; }

        /// <summary>
        /// Gets or sets the description
        /// </summary>
        [Column("descr")]
        public String Description { get; set; }
    }
}