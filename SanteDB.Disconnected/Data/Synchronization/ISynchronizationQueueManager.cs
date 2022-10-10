using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Synchronization queue manager
    /// </summary>
    public interface ISynchronizationQueueManager
    {

        /// <summary>
        /// Get all synchronization queues
        /// </summary>
        /// <param name="queueType">The type of queue to retrieve</param>
        /// <returns>The list of queues </returns>
        IEnumerable<ISynchronizationQueue> GetAll(SynchronizationPattern queueType);

        /// <summary>
        /// Get a specific synchronization queue
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        ISynchronizationQueue Get(String queueName);
    }
}
