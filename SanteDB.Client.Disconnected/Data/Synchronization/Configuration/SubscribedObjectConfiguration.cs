using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using System;
using System.Xml.Serialization;

namespace SanteDB.Client.Disconnected.Data.Synchronization.Configuration
{
    /// <summary>
    /// Represents a single resource which is subscribed
    /// </summary>
    [XmlType(nameof(SubscribedObjectConfiguration), Namespace = "http://santedb.org/configuration")]
    public class SubscribedObjectConfiguration : ResourceTypeReferenceConfiguration
    {
        /// <summary>
        /// Gets or sets the id of the type being subscribed to
        /// </summary>
        [XmlAttribute("id"), JsonProperty("id")]
        public Guid Identifier { get; set; }
    }
}