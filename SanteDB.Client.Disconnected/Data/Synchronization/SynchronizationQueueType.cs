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
        /// Outbound queue - The destination of this queue is to the upstream
        /// </summary>
        UpstreamToLocal = 0x1,
        /// <summary>
        /// Inbound queue - The source of the queue is the upstream
        /// </summary>
        LocalToUpstream = 0x2,
        /// <summary>
        /// The queue is both for inbound and outbound 
        /// </summary>
        BiDirectional = LocalToUpstream | UpstreamToLocal
    }
}
