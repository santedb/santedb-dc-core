using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Bluetooth
{
    public static class BluetoothConstants
    {
        /// <summary>
        /// Bluetooth service id
        /// </summary>
        public static readonly Guid P2P_SERVICE_ID = Guid.Parse("17FB1E15-D219-4192-91FD-CC00D5481254");

        /// <summary>
        /// Bluetooth device claim (attached to devices)
        /// </summary>
        public const string BluetoothDeviceClaim = "urn:santedb:org:claim:p2pnode";

        /// <summary>
        /// Trigger event for this request payload
        /// </summary>
        public const string BluetoothNodePairingTriggerEvent = "urn:santedb:org:p2p:bt:event:pair";

        /// <summary>
        /// Trigger event for this request payload
        /// </summary>
        public const string BluetoothNodeUnPairingTriggerEvent = "urn:santedb:org:p2p:bt:event:un-pair";

        /// <summary>
        /// Paring request message
        /// </summary>
        public const string BluetoothNodeParingRequest = "urn:santedb:org:p2p:bt:message:pair:request";

        /// <summary>
        /// Paring response message
        /// </summary>
        public const string BluetoothNodeParingResponse = "urn:santedb:org:p2p:bt:message:pair:response";

        /// <summary>
        /// Paring confirmation message
        /// </summary>
        public const string BluetoothNodeParingConfirmationRequest = "urn:santedb:org:p2p:bt:message:pair:confirm";

    }
}
