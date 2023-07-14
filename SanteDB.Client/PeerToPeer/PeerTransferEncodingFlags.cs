using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.PeerToPeer
{
    /// <summary>
    /// Encoding flags for the P2P transfer
    /// </summary>
    [Flags]
    public enum PeerTransferEncodingFlags
    {
        /// <summary>
        /// When enabled indicates the payload is compressed
        /// </summary>
        Compressed = 0x01

    }
}
