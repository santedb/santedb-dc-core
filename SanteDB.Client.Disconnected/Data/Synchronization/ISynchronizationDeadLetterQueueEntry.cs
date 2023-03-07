using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{

    /// <summary>
    /// Represents a retry entry
    /// </summary>
    public interface ISynchronizationDeadLetterQueueEntry : ISynchronizationQueueEntry
    {
        /// <summary>
        /// True if the object is a retry 
        /// </summary>
        String OriginalQueue { get; }

        /// <summary>
        /// Specialized tag data
        /// </summary>
        byte[] TagData { get; }
    }
}
