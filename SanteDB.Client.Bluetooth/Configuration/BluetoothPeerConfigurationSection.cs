using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Client.Bluetooth.Configuration
{
    /// <summary>
    /// Represents a bluetooth peer configuration
    /// </summary>
    [XmlType(nameof(BluetoothPeerConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class BluetoothPeerConfigurationSection : IEncryptedConfigurationSection
    {

        /// <summary>
        /// default ctor
        /// </summary>
        public BluetoothPeerConfigurationSection()
        {
            this.Timeout = 2000;
        }

        /// <summary>
        /// Gets or sets the timeout for sockets
        /// </summary>
        public int Timeout { get; set; }

    }
}
