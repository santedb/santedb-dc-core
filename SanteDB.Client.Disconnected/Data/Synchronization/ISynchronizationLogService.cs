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
 * Date: 2023-3-10
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a synchronization log service
    /// </summary>
    public interface ISynchronizationLogService
    {
        /// <summary>
        /// Get the last time that the specified type was synchronized
        /// </summary>
        DateTime? GetLastTime(Type modelType, String filter = null);

        /// <summary>
        /// Get the last ETag of the type
        /// </summary>
        String GetLastEtag(Type modelType, String filter = null);

        /// <summary>
        /// Update the log entry 
        /// </summary>
        void Save(Type modelType, String filter, String eTag, DateTime? since);

        /// <summary>
        /// Get all log entries
        /// </summary>
        IEnumerable<ISynchronizationLogEntry> GetAll();

        /// <summary>
        /// Save the specified query data for later continuation
        /// </summary>
        void SaveQuery(Type modelType, String filter, Guid queryId, int offset);

        /// <summary>
        /// Mark the specified query as complete
        /// </summary>
        void CompleteQuery(Type modelType, String filter, Guid queryId);
        /// <summary>
        /// Mark the specified query as complete
        /// </summary>
        void CompleteQuery(string modelType, String filter, Guid queryId);

        /// <summary>
        /// Find the query data
        /// </summary>
        ISynchronizationLogQuery FindQueryData(Type modelType, String filter);

        /// <summary>
        /// Deletes the specified log entry
        /// </summary>
        void Delete(ISynchronizationLogEntry itm);
    }
}
