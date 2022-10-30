using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model.Subscription;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Client.Disconnected.Data.Synchronization.Configuration
{
    /// <summary>
    /// Synchronization resource configuration
    /// </summary>
    [XmlType(nameof(SynchronizationResourceConfiguration), Namespace = "http://santedb.org/configuration")]
    public class SynchronizationResourceConfiguration 
    {
        /// <summary>
        /// The synchronization trigger event - when the synchronization of the resource should be triggered
        /// </summary>
        [XmlAttribute("trigger"), JsonProperty("trigger")]
        public SubscriptionTriggerType Trigger { get; set; }

        /// <summary>
        /// The data operation event - when the operation in the database triggers a synchronization push or pull
        /// </summary>
        [XmlAttribute("event"), JsonProperty("event")]
        public SynchronizationQueueEntryOperation DataOperation { get; set; }

        /// <summary>
        /// The identifier of the subscription which this applies to 
        /// </summary>
        [XmlAttribute("subsription"), JsonProperty("subscription")]
        public Guid SubscriptionDefinition { get; set; }

    }
}
