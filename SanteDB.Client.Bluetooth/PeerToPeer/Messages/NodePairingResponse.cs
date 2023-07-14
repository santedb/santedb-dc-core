using Newtonsoft.Json;
using SanteDB.Client.PeerToPeer;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security.Tfa;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Bluetooth.PeerToPeer.Messages
{
    /// <summary>
    /// Node pairing response
    /// </summary>
    internal class NodePairingResponse : IPeerToPeerMessagePayload
    {
        /// <summary>
        /// default ctor
        /// </summary>
        public NodePairingResponse()
        {
            
        }

        /// <summary>
        /// Create new payload with specified contents
        /// </summary>
        public NodePairingResponse(Rfc4226SecretClaim secretClaim)
        {
            this.AuthenticationSecret = secretClaim;
        }

        /// <inheritdoc/>
        [JsonIgnore]
        public string ContentType => "application/json";

        /// <inheritdoc/>
        [JsonIgnore]
        public string StructureIdentifier => BluetoothConstants.BluetoothNodeParingResponse;

        /// <summary>
        /// The device information from the remote
        /// </summary>
        [JsonProperty("node")]
        public BluetoothPeerNode Node { get; set; }

        /// <summary>
        /// Gets the authentication secret
        /// </summary>
        [JsonProperty("rfc4226")]
        public Rfc4226SecretClaim AuthenticationSecret { get; set; }

        /// <inheritdoc/>
        public void Populate(byte[] payloadData) => JsonConvert.PopulateObject(Encoding.UTF8.GetString(payloadData), this);

        /// <inheritdoc/>
        public byte[] Serialize() => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
    }
}
