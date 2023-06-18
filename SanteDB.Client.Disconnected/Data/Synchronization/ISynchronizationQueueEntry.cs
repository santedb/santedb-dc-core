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
