using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Synchronization
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
    }
}
