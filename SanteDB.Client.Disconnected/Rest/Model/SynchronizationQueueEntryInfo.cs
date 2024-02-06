using Newtonsoft.Json;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Text;
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
