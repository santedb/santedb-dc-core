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
