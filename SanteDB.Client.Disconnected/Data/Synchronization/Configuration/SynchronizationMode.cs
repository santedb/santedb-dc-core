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
        /// Synchronized based on subscriptions
        /// </summary>
        [XmlEnum("partial")]
        Partial = 0x1,
        /// <summary>
        /// Replicate everything
        /// </summary>
        [XmlEnum("replicate")]
        Full = 0x2
    }
}
