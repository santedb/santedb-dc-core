using DocumentFormat.OpenXml.Office2010.PowerPoint;
using InTheHand.Net;
using Newtonsoft.Json;
using SanteDB.Client.PeerToPeer;
using System;

namespace SanteDB.Client.Bluetooth.PeerToPeer
{
    /// <summary>
    /// An implementation of a <see cref="IPeerToPeerNode"/>
    /// </summary>
    [JsonObject]
    public class BluetoothPeerNode : IPeerToPeerNode
    {

        /// <summary>
        /// Creates a new bluetooth peer node
        /// </summary>
        public BluetoothPeerNode(Guid uuid, String deviceName, BluetoothAddress bluetoothAddress)
        {
            this.Uuid = uuid;
            this.Name = deviceName;
            this.BluetoothAddress = bluetoothAddress;
        }

        /// <inheritdoc/>
        [JsonProperty("uuid")]
        public Guid Uuid { get; set; }

        /// <inheritdoc/>
        [JsonProperty("name")]
        public string Name { get; set;  }

        /// <inheritdoc/>
        [JsonProperty("address")]
        public string Address
        {
            get => this.BluetoothAddress.ToByteArray().HexEncode();
            set => this.BluetoothAddress = new BluetoothAddress(value.HexDecode());
        }

        /// <summary>
        /// Gets the bluetooth address
        /// </summary>
        [JsonIgnore]
        public BluetoothAddress BluetoothAddress { get; private set; }
    }
}