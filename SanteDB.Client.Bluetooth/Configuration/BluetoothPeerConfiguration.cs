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
    [XmlType(nameof(BluetoothPeerConfiguration), Namespace = "http://santedb.org/configuration")]
    public class BluetoothPeerConfiguration : IEncryptedConfigurationSection
    {

    }
}
