using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.PeerToPeer
{
    /// <summary>
    /// Represents a payload object
    /// </summary>
    public interface IPeerToPeerMessagePayload
    {

        /// <summary>
        /// Gets the message structure events that this object supports
        /// </summary>
        string StructureIdentifier { get; }

        /// <summary>
        /// Serialize the payload to a byte array
        /// </summary>
        byte[] Serialize();

        /// <summary>
        /// Populate the object
        /// </summary>
        void Populate(byte[] payloadData);
    }

}
