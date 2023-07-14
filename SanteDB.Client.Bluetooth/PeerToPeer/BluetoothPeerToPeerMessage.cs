using SanteDB.Client.Bluetooth.PeerToPeer.Messages;
using SanteDB.Client.PeerToPeer;
using SanteDB.Client.PeerToPeer.Messages;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Security.Tfa;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SanteDB.Client.Bluetooth.PeerToPeer
{
    /// <summary>
    /// Bluetooth peer-to-peer message
    /// </summary>
    public class BluetoothPeerToPeerMessage : IPeerToPeerMessage
    {

        public BluetoothPeerToPeerMessage()
        {
            
        }

        /// <summary>
        /// Bluetooth peer-to-peer message
        /// </summary>
        private BluetoothPeerToPeerMessage(String triggerEvent, IPeerToPeerMessagePayload payload)
        {
            this.TriggerEvent = triggerEvent;
            this.Payload = payload;
            this.OriginationTime = DateTimeOffset.Now;
            this.Uuid = Guid.NewGuid();
        }

        /// <summary>
        /// Create a message for a pairing request
        /// </summary>
        internal static BluetoothPeerToPeerMessage NodePairRequest(String remoteUserName, String remotePassword, IPeerToPeerNode localNode)
            => new BluetoothPeerToPeerMessage(BluetoothConstants.BluetoothNodePairingTriggerEvent, new NodePairingRequest(remoteUserName, remotePassword, localNode.Name, localNode.Uuid));

        /// <summary>
        /// Create a node pairing result
        /// </summary>
        internal static BluetoothPeerToPeerMessage NodePairResponse(Rfc4226SecretClaim codeResult) =>
            new BluetoothPeerToPeerMessage(BluetoothConstants.BluetoothNodePairingTriggerEvent, new NodePairingResponse(codeResult));

        internal static BluetoothPeerToPeerMessage NodePairConfirm(Guid localDevice, String otpCode) =>
            new BluetoothPeerToPeerMessage(BluetoothConstants.BluetoothNodePairingTriggerEvent, new NodePairingConfirmationRequest(localDevice, otpCode));

        internal static BluetoothPeerToPeerMessage Ack(String message) =>
            new BluetoothPeerToPeerMessage(PeerToPeerConstants.AckTriggerEvent, new PeerAcknowledgmentPayload(PeerToPeerAcknowledgementCode.Ok, DetectedIssuePriorityType.Information, message));

        internal static BluetoothPeerToPeerMessage Nack(String message) =>
            new BluetoothPeerToPeerMessage(PeerToPeerConstants.AckTriggerEvent, new PeerAcknowledgmentPayload(PeerToPeerAcknowledgementCode.Error, DetectedIssuePriorityType.Error, message));

        /// <summary>
        /// Create a message for a pairing request
        /// </summary>
        internal static BluetoothPeerToPeerMessage NodeUnPairRequest(String remoteUserName, String remotePassword, IPeerToPeerNode localNode)
            => new BluetoothPeerToPeerMessage(BluetoothConstants.BluetoothNodeUnPairingTriggerEvent, new NodePairingRequest(remoteUserName, remotePassword, localNode.Name, localNode.Uuid));

        /// <inheritdoc/>
        public Guid Uuid { get; set;  }

        /// <inheritdoc/>
        public Guid OriginNode { get; set; }

        /// <inheritdoc/>
        public Guid DestinationNode { get; set; }

        /// <inheritdoc/>
        public DateTimeOffset OriginationTime { get; set; }

        /// <inheritdoc/>
        public string TriggerEvent { get; set;  }

        /// <inheritdoc/>
        public IPeerToPeerMessagePayload Payload { get; set; }

    }
}
