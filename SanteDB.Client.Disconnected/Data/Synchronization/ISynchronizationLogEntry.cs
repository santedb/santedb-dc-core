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
        /// Gets the type of resource that this entry referrs.
        /// </summary>
        string ResourceType { get; }

        /// <summary>
        /// Gets the filter that applies to this entry. The filter may be null when no filter for the resource is specified.
        /// </summary>
        string Filter { get; }

        /// <summary>
        /// Gets the last sync time that this was successfully completed. 
        /// </summary>
        DateTime LastSync { get; }

        /// <summary>
        /// Get the last etag that was successfully fetched.
        /// </summary>
        string LastETag { get; }

        
    }
}
