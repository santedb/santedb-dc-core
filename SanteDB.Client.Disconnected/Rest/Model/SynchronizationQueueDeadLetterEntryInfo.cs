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
 * User: fyfej
 * Date: 2024-1-23
 */
using Newtonsoft.Json;
using SanteDB.Client.Disconnected.Data.Synchronization;
using System.Xml.Serialization;

namespace SanteDB.Client.Disconnected.Rest.Model
{
    /// <summary>
    /// A serialization wrapper for <see cref="ISynchronizationQueueEntry"/>
    /// </summary>
    [XmlType(nameof(SynchronizationQueueDeadLetterEntryInfo), Namespace = "http://santedb.org/ami")]
    [XmlRoot(nameof(SynchronizationQueueDeadLetterEntryInfo), Namespace = "http://santedb.org/ami")]
    [JsonObject(nameof(SynchronizationQueueDeadLetterEntryInfo))]
    public class SynchronizationQueueDeadLetterEntryInfo : SynchronizationQueueEntryInfo
    {

        /// <summary>
        /// Synchronization queue entry
        /// </summary>
        public SynchronizationQueueDeadLetterEntryInfo()
        {
        }

        /// <summary>
        /// Create a new queue entry information based on <paramref name="synchronizationQueueEntry"/>
        /// </summary>
        public SynchronizationQueueDeadLetterEntryInfo(ISynchronizationDeadLetterQueueEntry synchronizationQueueEntry) : base(synchronizationQueueEntry)
        {
            this.OriginalQueue = new SynchronizationQueueInfo(synchronizationQueueEntry.OriginalQueue);
            this.ReasonForRejection = synchronizationQueueEntry.ReasonForRejection;
        }

        /// <summary>
        /// Gets or sets the original queue name
        /// </summary>
        [JsonProperty("originalQueue"), XmlElement("originalQueue")]
        public SynchronizationQueueInfo OriginalQueue { get; set; }

        /// <summary>
        /// Gets or sets the reason for rejection
        /// </summary>
        [JsonProperty("reason"), XmlElement("reason")]
        public string ReasonForRejection { get; set; }
    }
}
