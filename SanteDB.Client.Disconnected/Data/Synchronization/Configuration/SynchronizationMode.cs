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
        [XmlEnum("sub")]
        Subscriptions = 0x1,
        /// <summary>
        /// Operate online only - no synchronization
        /// </summary>
        [XmlEnum("none")]
        Online = 0x2,
        /// <summary>
        /// Replicate everything
        /// </summary>
        [XmlEnum("replicate")]
        All = 0x4
    }
}
