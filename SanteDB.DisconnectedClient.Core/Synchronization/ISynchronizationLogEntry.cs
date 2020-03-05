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
using System;

namespace SanteDB.DisconnectedClient.Core.Synchronization
{
    /// <summary>
    /// Represents a log entry for synchronization
    /// </summary>
    public interface ISynchronizationLogEntry
    {

        /// <summary>
        /// Gets the type of resource
        /// </summary>
        string ResourceType { get; }

        /// <summary>
        /// Gets the last sync time
        /// </summary>
        DateTime LastSync { get; }

        /// <summary>
        /// Get the last etag
        /// </summary>
        string LastETag { get; }

        /// <summary>
        /// Gets the filter
        /// </summary>
        string Filter { get; }
    }

    /// <summary>
    /// Represents a specialized log entry which is a query
    /// </summary>
    public interface ISynchronizationLogQuery : ISynchronizationLogEntry
    {
        /// <summary>
        /// Gets or sets the UUID of the query
        /// </summary>
        Guid Uuid { get; set; }

        /// <summary>
        /// Last successful record number
        /// </summary>
        int LastSuccess { get; set; }

        /// <summary>
        /// Start time of the query
        /// </summary>
        DateTime StartTime { get; set; }
    }
}
