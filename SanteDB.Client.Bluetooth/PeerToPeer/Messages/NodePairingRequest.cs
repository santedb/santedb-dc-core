using Newtonsoft.Json;
using SanteDB.Client.PeerToPeer;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Bluetooth.PeerToPeer.Messages
{
    /// <summary>
    /// Node pairing request
    /// </summary>
    internal class NodePairingRequest : IPeerToPeerMessagePayload
    {
        
        /// <summary>
        /// Pairing request serialization ctor
        /// </summary>
        public NodePairingRequest()
        {
        }

        /// <summary>
        /// Create new node pairing request
        /// </summary>
        public NodePairingRequest(String userName, String password, String deviceName, Guid deviceSid)
        {
            this.UserName = userName;
            this.Password = password;
            this.DeviceName = deviceName;
            this.DeviceSid = deviceSid;
        }

        /// <inheritdoc/>
        [JsonIgnore]
        public string ContentType => "application/json";

        /// <inheritdoc/>
        [JsonIgnore]
        public string StructureIdentifier => BluetoothConstants.BluetoothNodeParingRequest;

        /// <summary>
        /// Gets or sets the username on the remote system
        /// </summary>
        [JsonProperty("user")]
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the remote password
        /// </summary>
        [JsonProperty("password")]
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the device name
        /// </summary>
        [JsonProperty("nodeName")]
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the device SID
        /// </summary>
        [JsonProperty("nodeSid")]
        public Guid DeviceSid { get; }

        /// <inheritdoc/>
        public void Populate(byte[] payloadData) => JsonConvert.PopulateObject(Encoding.UTF8.GetString(payloadData), this);

        /// <inheritdoc/>
        public byte[] Serialize() => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
    }
}
