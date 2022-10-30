using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a synchronization queue entry
    /// </summary>
    public interface ISynchronizationQueueEntry
    {

        /// <summary>
        /// Gets the identifier of the queue entry
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// Gets the time that the entry was created
        /// </summary>
        DateTime CreationTime { get; set; }

        /// <summary>
        /// Gets the type of data
        /// </summary>
        String Type { get; set; }

        /// <summary>
        /// Gets the data of the object
        /// </summary>
        String DataFileKey { get; set; }

        /// <summary>
        /// Gets or sets the transient data
        /// </summary>
        IdentifiedData Data { get; set; }

        /// <summary>
        /// Gets the operation of the object
        /// </summary>
        SynchronizationQueueEntryOperation Operation { get; set; }

        /// <summary>
        /// Get whether the object is a retry
        /// </summary>
        bool IsRetry { get; set; }
    }


}
