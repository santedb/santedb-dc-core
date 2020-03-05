/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2019-11-27
 */
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using System;
using System.Collections.Generic;

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
        SynchronizationOperationType Operation { get; set; }

        /// <summary>
        /// Get whether the object is a retry
        /// </summary>
        bool IsRetry { get; set; }
    }

    /// <summary>
    /// Represents a retry entry
    /// </summary>
    public interface ISynchronizationQueueRetryEntry : ISynchronizationQueueEntry
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

    /// <summary>
    /// Represents a synchronization queue
    /// </summary>
    public interface ISynchronizationQueue
    {

        /// <summary>
        /// Fired prior to data being placed into this queue
        /// </summary>
        event EventHandler<DataPersistingEventArgs<ISynchronizationQueueEntry>> Enqueuing;

        /// <summary>
        /// Fired after data has been placed into this queue
        /// </summary>
        event EventHandler<DataPersistedEventArgs<ISynchronizationQueueEntry>> Enqueued;

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

        /// <summary>
        /// Requeue the queue item
        /// </summary>
        void Retry(ISynchronizationQueueRetryEntry queueItem);


        /// <summary>
        /// Query the dataset from the specified search parameters
        /// </summary>
        IEnumerable<ISynchronizationQueueEntry> Query(NameValueCollection search, int offset, int count, out int totalResults);
    }
}
