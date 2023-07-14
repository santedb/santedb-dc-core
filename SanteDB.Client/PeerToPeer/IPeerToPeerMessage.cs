using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.PeerToPeer
{

    /// <summary>
    /// A generic peer-to-peer message
    /// </summary>
    public interface IPeerToPeerMessage
    {
        /// <summary>
        /// Gets the UUID of the message
        /// </summary>
        Guid Uuid { get; set; }

        /// <summary>
        /// Gets the UUID of the origin
        /// </summary>
        Guid OriginNode { get; set; }

        /// <summary>
        /// Gets the UUID of the endpoint
        /// </summary>
        Guid DestinationNode { get; set; }

        /// <summary>
        /// Gets the origination time of the message
        /// </summary>
        DateTimeOffset OriginationTime { get; set; }

        /// <summary>
        /// Gets the trigger event for the message
        /// </summary>
        string TriggerEvent { get; set; }

        /// <summary>
        /// The payload of the message event
        /// </summary>
        IPeerToPeerMessagePayload Payload { get; set; }
    }

}
