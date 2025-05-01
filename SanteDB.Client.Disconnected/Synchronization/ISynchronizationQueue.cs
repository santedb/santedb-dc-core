/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 */
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Attributes;
using SanteDB.Core.Model.Query;
using System;
using System.Linq.Expressions;

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
        [QueryParameter("name")]
        String Name { get; }

        /// <summary>
        /// Gets the type of the queue
        /// </summary>
        [QueryParameter("type")]
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
        /// Enqueue the data
        /// </summary>
        /// <param name="data">The data to enqueue</param>
        /// <param name="operation">The sync operation </param>
        ISynchronizationQueueEntry Enqueue(IdentifiedData data, SynchronizationQueueEntryOperation operation);

        /// <summary>
        /// Enqueues the specified <paramref name="otherQueueEntry"/> directly into this queue (used for copying queue entries) with a reason 
        /// </summary>
        /// <param name="otherQueueEntry">The queue entry which should be enqueued in this queue</param>
        /// <param name="reasonText">The reason for the other queue entry being placed onto this queue</param>
        /// <remarks>
        /// Implementers should not make assumptions that this queue entry has been de-queued and should preserve the original <paramref name="otherQueueEntry"/> data 
        /// if they are using a shared record of data files. Typically a "retry" occurs when a queue entry of type <see cref="ISynchronizationDeadLetterQueueEntry"/> is
        /// being queued into a queue without the <see cref="SynchronizationPattern.DeadLetter"/> attribute, if an entry of <see cref="ISynchronizationQueueEntry"/> is being
        /// enqueued to a queue with <see cref="SynchronizationPattern.DeadLetter"/> attribute then it represents a "cannot deliver" condition.
        /// </remarks>
        /// <returns>The newly created queue entry</returns>
        ISynchronizationQueueEntry Enqueue(ISynchronizationQueueEntry otherQueueEntry, String reasonText = null);

        /// <summary>
        /// Peek the next item on the queue
        /// </summary>
        /// <returns>The next item on the queue</returns>
        ISynchronizationQueueEntry Peek();

        /// <summary>
        /// Dequeue the item from the queue
        /// </summary>
        /// <returns>The queued item</returns>
        ISynchronizationQueueEntry Dequeue();

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
        /// Query the dataset from the specified search parameters
        /// </summary>
        IQueryResultSet<ISynchronizationQueueEntry> Query(Expression<Func<ISynchronizationQueueEntry, bool>> query);

    }
}
