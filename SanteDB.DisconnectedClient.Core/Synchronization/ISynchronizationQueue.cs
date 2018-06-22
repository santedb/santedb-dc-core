﻿using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Synchronization
{
    /// <summary>
    /// Represents a synchronization queue entry
    /// </summary>
    public interface ISynchronizationQueueEntry
    {

        /// <summary>
        /// Gets the identifier of the queue entry
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the time that the entry was created
        /// </summary>
        DateTime CreationTime { get; }

        /// <summary>
        /// Gets the type of data
        /// </summary>
        String Type { get; }

        /// <summary>
        /// Gets the data of the object
        /// </summary>
        String Data { get; }

        /// <summary>
        /// Gets the operation of the object
        /// </summary>
        SynchronizationOperationType Operation { get; }

    }

    /// <summary>
    /// Represents a synchronization queue
    /// </summary>
    public interface ISynchronizationQueue
    {

        /// <summary>
        /// Enqueue the data
        /// </summary>
        /// <param name="data">The data to enqueue</param>
        /// <param name="operation">The sync operation </param>
        ISynchronizationQueueEntry Enqueue(IdentifiedData data, SynchronizationOperationType operation);

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
        /// Get all queue items from the queue
        /// </summary>
        /// <param name="offset">The starting offset of objects</param>
        /// <param name="count">The number of objects to retrieve</param>
        /// <returns>The raw queue items</returns>
        IEnumerable<ISynchronizationQueueEntry> GetAll(int offset, int count, out int totalResults);

    }
}
