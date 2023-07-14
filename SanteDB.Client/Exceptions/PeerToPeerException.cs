using SanteDB.Client.PeerToPeer;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Exceptions
{
    /// <summary>
    /// Represents a peer-to-peer exception
    /// </summary>
    public class PeerToPeerException : Exception
    {

        /// <summary>
        /// Creates a new peer to peer exception
        /// </summary>
        public PeerToPeerException(IPeerToPeerNode origin, IPeerToPeerNode destination, String detail, Exception cause) : base($"Peer-to-peer error from {origin.Name} to {destination.Name} - {detail}", cause)
        {
            this.Origin = origin;
            this.Destination = destination;
            this.Detail = detail;
        }

        /// <summary>
        /// Gets the originating peer
        /// </summary>
        public IPeerToPeerNode Origin { get; }

        /// <summary>
        /// Gets the destination peer
        /// </summary>
        public IPeerToPeerNode Destination { get; }

        /// <summary>
        /// Gets the detail error
        /// </summary>
        public string Detail { get; }
    }
}
