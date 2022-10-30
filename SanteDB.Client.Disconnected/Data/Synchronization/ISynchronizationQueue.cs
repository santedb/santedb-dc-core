using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a synchronization queue data source
    /// </summary>
    public interface ISynchronizationQueue
    {

        /// <summary>
        /// Gets the name of the queue
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Gets the type of the queue
        /// </summary>
        SynchronizationPattern Type { get; }

        /// <summary>
        /// Fired prior to data being placed into this queue
        /// </summary>
        event EventHandler<DataPersistingEventArgs<ISynchronizationQueueEntry>> Enqueuing;

        /// <summary>
        /// Fired after data has been placed into this queue
        /// </summary>
        event EventHandler<DataPersistedEventArgs<ISynchronizationQueueEntry>> Enqueued;

        /// <summary>
        /// Fired when the queue has been exhausted
        /// </summary>
        event EventHandler<QueueExhaustedEventArgs> Exhausted;

        /// <summary>
        /// Enqueue the data
        /// </summary>
        /// <param name="data">The data to enqueue</param>
        /// <param name="operation">The sync operation </param>
        ISynchronizationQueueEntry Enqueue(IdentifiedData data, SynchronizationQueueEntryOperation operation);

        /// <summary>
        /// Peek the next item on the queue
        /// </summary>
        /// <returns>The next item on the queue</returns>
        IdentifiedData Peek();

        /// <summary>
        /// Dequeue the item from the queue
        /// </summary>
        /// <returns>The queued item</returns>
        IdentifiedData Dequeue();

        /// <summary>
        /// Get the count of objects on the queue
        /// </summary>
        int Count();

        /// <summary>
        /// Delete an item from the queue
        /// </summary>
        /// <param name="id">The id of the item in the queue to delete</param>
        void Delete(int id);

        /// <summary>
        /// Get the data from the queue
        /// </summary>
        /// <param name="id">The identifier of the queue object</param>
        /// <returns>The queue object</returns>
        ISynchronizationQueueEntry Get(int id);

        /// <summary>
        /// Requeue the queue item
        /// </summary>
        void Retry(ISynchronizationDeadLetterQueueEntry queueItem);

        /// <summary>
        /// Query the dataset from the specified search parameters
        /// </summary>
        IQueryResultSet<ISynchronizationQueueEntry> Query(NameValueCollection search);
    }
}
