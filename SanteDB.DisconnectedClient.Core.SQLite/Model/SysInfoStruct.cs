using SQLite.Net.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Model
{
    /// <summary>
    /// Information structure
    /// </summary>
    public class SysInfoStruct
    {
        [Column("name")]
        public String Name { get; set; }
    }
}
