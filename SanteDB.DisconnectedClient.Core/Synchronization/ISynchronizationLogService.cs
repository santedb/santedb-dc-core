/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-6-28
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Synchronization
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
        void Save(Type modelType, String filter, String eTag, String name);

        /// <summary>
        /// Get all log entries
        /// </summary>
        List<ISynchronizationLogEntry> GetAll();

        /// <summary>
        /// Save the specified query data for later continuation
        /// </summary>
        void SaveQuery(Type modelType, String filter, Guid queryId, String name, int offset);

        /// <summary>
        /// Mark the specified query as complete
        /// </summary>
        void CompleteQuery(Guid queryId);

        /// <summary>
        /// Find the query data
        /// </summary>
        ISynchronizationLogQuery FindQueryData(Type modelType, String filter);
    }
}
