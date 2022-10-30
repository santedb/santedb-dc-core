using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a log entry for synchronization
    /// </summary>
    public interface ISynchronizationLogEntry
    {

        /// <summary>
        /// Gets the type of resource
        /// </summary>
        string ResourceType { get; }

        /// <summary>
        /// Gets the last sync time
        /// </summary>
        DateTime LastSync { get; }

        /// <summary>
        /// Get the last etag
        /// </summary>
        string LastETag { get; }

        /// <summary>
        /// Gets the filter
        /// </summary>
        string Filter { get; }
    }

    /// <summary>
    /// Represents a specialized log entry which is a query
    /// </summary>
    public interface ISynchronizationLogQuery : ISynchronizationLogEntry
    {
        /// <summary>
        /// Gets or sets the UUID of the query
        /// </summary>
        Guid Uuid { get; set; }

        /// <summary>
        /// Last successful record number
        /// </summary>
        int LastSuccess { get; set; }

        /// <summary>
        /// Start time of the query
        /// </summary>
        DateTime StartTime { get; set; }
    }
}
