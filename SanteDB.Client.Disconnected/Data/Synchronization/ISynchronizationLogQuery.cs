using System;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a specialized log entry which is a query
    /// </summary>
    public interface ISynchronizationLogQuery : ISynchronizationLogEntry
    {
        /// <summary>
        /// Gets or sets the UUID of the query
        /// </summary>
        Guid QueryId { get; set; }

        /// <summary>
        /// Last successful record number
        /// </summary>
        int QueryOffset { get; set; }

        /// <summary>
        /// Start time of the query
        /// </summary>
        DateTime QueryStartTime { get; set; }
    }
}
