using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Client.Disconnected.Data.Synchronization.Configuration
{

    /// <summary>
    /// Synchronization mode
    /// </summary>
    [XmlType(nameof(SynchronizationMode), Namespace = "http://santedb.org/configuration")]
    public enum SynchronizationMode
    {
        /// <summary>
        /// Synchronization mode - Cache results offline
        /// </summary>
        [XmlEnum("sync")]
        Sync = 0x1,
        /// <summary>
        /// Operate online only
        /// </summary>
        [XmlEnum("online")]
        Online = 0x2,
        /// <summary>
        /// Operate offline only
        /// </summary>
        [XmlEnum("offline")]
        Offline = 0x4
    }
}
