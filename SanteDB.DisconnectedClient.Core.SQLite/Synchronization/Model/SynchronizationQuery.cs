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
 * Date: 2018-8-25
 */
using SanteDB.DisconnectedClient.Core.Synchronization;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Synchronization.Model
{
    /// <summary>
    /// Synchroinzation query exec
    /// </summary>
    [Table("sync_queue")]
    public class SynchronizationQuery : SynchronizationLogEntry, ISynchronizationLogQuery
    {


        /// <summary>
        /// Last successful record number
        /// </summary>
        [Column("last_recno")]
        public int LastSuccess { get; set; }

        /// <summary>
        /// Start time of the query
        /// </summary>
        [Column("start")]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the UUID
        /// </summary>
        [Ignore]
        Guid ISynchronizationLogQuery.Uuid
        {
            get => new Guid(this.Uuid);
            set => this.Uuid = value.ToByteArray();
        }
    }
}
