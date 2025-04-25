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
using Newtonsoft.Json;
using SanteDB.Client.Disconnected.Data.Synchronization;
using System;
using System.Xml.Serialization;

namespace SanteDB.Client.Disconnected.Rest.Model
{
    /// <summary>
    /// A serialization wrapper for <see cref="ISynchronizationQueueEntry"/>
    /// </summary>
    [XmlType(nameof(SynchronizationQueueEntryInfo), Namespace = "http://santedb.org/ami")]
    [XmlRoot(nameof(SynchronizationQueueEntryInfo), Namespace = "http://santedb.org/ami")]
    [JsonObject(nameof(SynchronizationQueueEntryInfo))]
    public class SynchronizationQueueEntryInfo
    {

        /// <summary>
        /// Synchronization queue entry
        /// </summary>
        public SynchronizationQueueEntryInfo()
        {
        }

        /// <summary>
        /// Create a new queue entry information based on <paramref name="synchronizationQueueEntry"/>
        /// </summary>
        public SynchronizationQueueEntryInfo(ISynchronizationQueueEntry synchronizationQueueEntry)
        {
            this.Id = synchronizationQueueEntry.Id;
            this.CorrelationKey = synchronizationQueueEntry.CorrelationKey;
            this.CreationTime = synchronizationQueueEntry.CreationTime.DateTime;
            this.ResourceType = synchronizationQueueEntry.ResourceType;
            this.DataFileKey = synchronizationQueueEntry.DataFileKey;
            this.Operation = synchronizationQueueEntry.Operation;
            this.RetryCount = synchronizationQueueEntry.RetryCount.GetValueOrDefault();
            this.Queue = new SynchronizationQueueInfo(synchronizationQueueEntry.Queue);
        }

        /// <summary>
        /// Gets or sets the identifier for the queue entry
        /// </summary>
        [XmlElement("id"), JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the correlation key entry
        /// </summary>
        [XmlElement("correlationKey"), JsonProperty("correlationKey")]
        public Guid CorrelationKey { get; set; }

        /// <summary>
        /// Gets or sets the creation time
        /// </summary>
        [XmlElement("creationTime"), JsonProperty("creationTime")]
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Gets or sets the resource type
        /// </summary>
        [XmlElement("resourceType"), JsonProperty("resourceType")]
        public string ResourceType { get; set; }

        /// <summary>
        /// Gets or sets the data file key
        /// </summary>
        [XmlElement("dataFile"), JsonProperty("dataFile")]
        public Guid DataFileKey { get; set; }

        /// <summary>
        /// Gets or sets the operation 
        /// </summary>
        [XmlElement("operation"), JsonProperty("operation")]
        public SynchronizationQueueEntryOperation Operation { get; set; }

        /// <summary>
        /// Gets or sets the retry count
        /// </summary>
        [XmlElement("retry"), JsonProperty("retry")]
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the queue
        /// </summary>
        [XmlElement("queue"), JsonProperty("queue")]
        public SynchronizationQueueInfo Queue { get; set; }
    }
}
