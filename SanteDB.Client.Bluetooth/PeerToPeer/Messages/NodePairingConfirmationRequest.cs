using Hl7.Fhir.Model;
using Newtonsoft.Json;
using SanteDB.Client.PeerToPeer;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Bluetooth.PeerToPeer.Messages
{
    /// <summary>
    /// Node pairing confirmation with the remote device
    /// </summary>
    internal class NodePairingConfirmationRequest : IPeerToPeerMessagePayload
    {
        public NodePairingConfirmationRequest()
        {
            
        }

        public NodePairingConfirmationRequest(Guid nodeSid, String otpResponse)
        {
            this.NodeSid = nodeSid;
            this.OneTimeCode= otpResponse;
        }
        /// <inheritdoc/>
        [JsonIgnore]
        public string ContentType => "application/json";

        /// <inheritdoc/>
        [JsonIgnore]
        public string StructureIdentifier => BluetoothConstants.BluetoothNodeParingConfirmationRequest;

        /// <summary>
        /// The sid for the pairing request
        /// </summary>
        [JsonProperty("sid")]
        public Guid NodeSid { get; set; }

        /// <summary>
        /// One time code for the pairing request
        /// </summary>
        [JsonProperty("otc")]
        public String OneTimeCode { get; set; }

        /// <inheritdoc/>
        public void Populate(byte[] payloadData) => JsonConvert.PopulateObject(Encoding.UTF8.GetString(payloadData), this);

        /// <inheritdoc/>
        public byte[] Serialize() => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
    }
}
