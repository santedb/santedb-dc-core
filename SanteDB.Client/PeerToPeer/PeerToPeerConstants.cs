using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.PeerToPeer
{
    /// <summary>
    /// Peer to peer constants
    /// </summary>
    public static class PeerToPeerConstants
    {
        /// <summary>
        /// Message structure Id for generic acks
        /// </summary>
        public const string AckMessageStructureId = "urn:santedb:org:p2p:message:ack";

        /// <summary>
        /// Message trigger event for echo/ping
        /// </summary>
        public const string EchoPingTriggerEvent = "urn:santedb:org:p2p:event:ping";

        /// <summary>
        /// General acknowledgement
        /// </summary>
        public const string AckTriggerEvent = "urn:santedb:org:p2p:event:ack";
    }
}
