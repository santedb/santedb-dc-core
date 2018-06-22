using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Synchronization
{
    /// <summary>
    /// Synchronization operation type.
    /// </summary>
    public enum SynchronizationOperationType
    {
        /// <summary>
        /// The operation represents an inbound entry (sync)
        /// </summary>
        Sync = 0,
        /// <summary>
        /// Operation represents an insert (create) only if not existing
        /// </summary>
        Insert = 1,
        /// <summary>
        /// Operation represents an update
        /// </summary>
        Update = 2,
        /// <summary>
        /// Operation represents an obsolete
        /// </summary>
        Obsolete = 3
    }
}
