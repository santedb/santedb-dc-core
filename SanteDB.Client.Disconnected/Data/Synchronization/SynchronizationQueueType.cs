using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Classification of the synchronization type
    /// </summary>
    [Flags]
    public enum SynchronizationPattern
    {
        /// <summary>
        /// Inbound queue - The source of the queue is the upstream
        /// </summary>
        UpstreamToLocal = 0x1,
        /// <summary>
        /// Outbound queue - The destination of this queue is to the upstream
        /// </summary>
        LocalToUpstream = 0x2,
        /// <summary>
        /// The queue is for local-local communication
        /// </summary>
        LocalOnly = 0x4,
        /// <summary>
        /// The queue is both for inbound and outbound 
        /// </summary>
        BiDirectional = LocalToUpstream | UpstreamToLocal,
        /// <summary>
        /// The queue is a deadletter queue.
        /// </summary>
        DeadLetter = 0xF | LocalOnly

    }
}
