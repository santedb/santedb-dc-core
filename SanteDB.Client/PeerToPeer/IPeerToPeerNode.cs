using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.PeerToPeer
{
    /// <summary>
    /// Implementers of this interface store data related to a peer-to-peer recipient
    /// </summary>
    public interface IPeerToPeerNode
    {
        /// <summary>
        /// Gets the unique identifier for the P2P recipient
        /// </summary>
        Guid Uuid { get; }

        /// <summary>
        /// Gets the name of the P2P recipient
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the provider reference of the P2P recipient
        /// </summary>
        string Address { get; }

    }

}
