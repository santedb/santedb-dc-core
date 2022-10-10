using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Synchronization operation type.
    /// </summary>
    public enum SynchronizationTriggerEventType
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
        Obsolete = 4,
        /// <summary>
        /// All actions
        /// </summary>
        All = Insert | Update | Obsolete
    }
}
