/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using SanteDB.Client.Disconnected.Data.Synchronization;
using System;

namespace SanteDB.Client.Disconnected.Exceptions
{
    /// <summary>
    /// Represents a <see cref="Exception"/> caused by an interaction with q synchronization queue
    /// </summary>
    public class SynchronizationQueueException : Exception
    {

        /// <summary>
        /// Gets the queue that raised this exception
        /// </summary>
        public ISynchronizationQueue Queue { get; }

        /// <summary>
        /// Creates a new synchronization queue exception with the specified <paramref name="queue"/> and <paramref name="message"/>
        /// </summary>
        /// <param name="queue">The queue that is raising the exception</param>
        /// <param name="message">The message of the exception</param>
        public SynchronizationQueueException(ISynchronizationQueue queue, String message) : this(queue, message, null)
        {
        }

        /// <summary>
        /// Creates a new synchronization queue exception with a specified <paramref name="innerException"/>
        /// </summary>
        /// <param name="queue">The queue which is raising the exception</param>
        /// <param name="message">The message for the exception</param>
        /// <param name="innerException">The exception which caused this</param>
        public SynchronizationQueueException(ISynchronizationQueue queue, String message, Exception innerException) : base(message, innerException)
        {
            this.Queue = queue;
        }
    }
}
