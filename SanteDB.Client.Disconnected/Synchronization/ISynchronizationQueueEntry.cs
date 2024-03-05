/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 * User: fyfej
 * Date: 2023-5-19
 */
using SanteDB.Core.Model;
using SanteDB.Core.Model.Attributes;
using System;

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
        [QueryParameter("id")]
        int Id { get; }

        /// <summary>
        /// A uuid which correlates this queue entry throughout its lifecycle
        /// </summary>
        [QueryParameter("correlation")]
        Guid CorrelationKey { get; }

        /// <summary>
        /// Gets the time that the entry was created
        /// </summary>
        [QueryParameter("creationTime")]
        DateTimeOffset CreationTime { get; }

        /// <summary>
        /// Gets the type of data
        /// </summary>
        [QueryParameter("type")]
        String ResourceType { get; }

        /// <summary>
        /// Gets the data of the object
        /// </summary>
        [QueryParameter("dataFile")]
        Guid DataFileKey { get; }

        /// <summary>
        /// Gets or sets the transient data
        /// </summary>
        [QueryParameter("data")]
        IdentifiedData Data { get; }

        /// <summary>
        /// Gets the operation of the object
        /// </summary>
        [QueryParameter("operation")]
        SynchronizationQueueEntryOperation Operation { get; }

        /// <summary>
        /// Get whether the object is a retry
        /// </summary>
        [QueryParameter("retry")]
        int? RetryCount { get; }

        /// <summary>
        /// Gets the queue 
        /// </summary>
        [QueryParameter("queue")]
        ISynchronizationQueue Queue { get; }
    }

}
