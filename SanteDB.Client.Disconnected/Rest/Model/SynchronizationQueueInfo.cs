using Newtonsoft.Json;
using SanteDB.Client.Disconnected.Data.Synchronization;
using System.Xml.Serialization;

namespace SanteDB.Client.Disconnected.Rest.Model
{
    /// <summary>
    /// Wrapper for REST interactions of <see cref="ISynchronizationQueue"/>
    /// </summary>
    [XmlType(nameof(SynchronizationQueueInfo), Namespace = "http://santedb.org/ami")]
    public class SynchronizationQueueInfo
    {

        public SynchronizationQueueInfo()
        {

        }

        public SynchronizationQueueInfo(ISynchronizationQueue synchronizationQueue)
        {
            this.Name = synchronizationQueue.Name;
            this.Pattern = synchronizationQueue.Type;
        }

        /// <summary>
        /// Gets or sets the name of the queue
        /// </summary>
        [XmlElement("name"), JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the pattern
        /// </summary>
        [XmlElement("pattern"), JsonProperty("pattern")]
        public SynchronizationPattern Pattern { get; set; }
    }
}