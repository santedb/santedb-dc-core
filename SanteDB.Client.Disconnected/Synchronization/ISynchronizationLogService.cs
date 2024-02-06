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
using System;
using System.Collections.Generic;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a synchronization log service
    /// </summary>
    public interface ISynchronizationLogService
    {

        /// <summary>
        /// Create a new synchronization log entry
        /// </summary>
        ISynchronizationLogEntry Create(Type modelType, String filter = null);

        /// <summary>
        /// Get the last time that the specified type was synchronized
        /// </summary>
        ISynchronizationLogEntry Get(Type modelType, String filter = null);

        /// <summary>
        /// Update the log entry 
        /// </summary>
        ISynchronizationLogEntry Save(ISynchronizationLogEntry entry, String eTag, DateTimeOffset? since);

        /// <summary>
        /// Save the error to the synchronization log
        /// </summary>
        ISynchronizationLogEntry SaveError(ISynchronizationLogEntry entry, Exception exception);

        /// <summary>
        /// Get all log entries
        /// </summary>
        IEnumerable<ISynchronizationLogEntry> GetAll();

        /// <summary>
        /// Start a query record
        /// </summary>
        ISynchronizationLogQuery StartQuery(ISynchronizationLogEntry entry);
        /// <summary>
        /// Save the specified query data for later continuation
        /// </summary>
        ISynchronizationLogQuery SaveQuery(ISynchronizationLogQuery query, int offset);

        /// <summary>
        /// Mark the specified query as complete
        /// </summary>
        void CompleteQuery(ISynchronizationLogQuery query);

        /// <summary>
        /// Find the query data
        /// </summary>
        ISynchronizationLogQuery GetCurrentQuery(ISynchronizationLogEntry entry);

        /// <summary>
        /// Deletes the specified log entry
        /// </summary>
        void Delete(ISynchronizationLogEntry itm);
    }
}
