﻿/*
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
using Newtonsoft.Json;
using SanteDB.Core.Model;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Synchronization;
using SQLite.Net.Attributes;
using System;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.SQLite.Synchronization.Model
{

    /// <summary>
    /// The message queue represents outbound or inbound data requests found by the sync service
    /// </summary>
    [JsonObject(nameof(SynchronizationQueueEntry)), XmlType(nameof(SynchronizationQueueEntry), Namespace = "http://santedb.org/queue")]
    public abstract class SynchronizationQueueEntry : ISynchronizationQueueEntry
    {

        // Gets the transient data
        private IdentifiedData m_transientData = null;

        /// <summary>
        /// Serialization ctor
        /// </summary>
        public SynchronizationQueueEntry()
        {

        }

        /// <summary>
        /// Copy ctor
        /// </summary>
        public SynchronizationQueueEntry(SynchronizationQueueEntry entry)
        {
            this.CreationTime = entry.CreationTime;
            this.DataFileKey = ApplicationContext.Current.GetService<IQueueFileProvider>().CopyQueueData(entry.DataFileKey);
            this.Data = entry.Data;
            this.IsRetry = entry.IsRetry;
            this.Operation = entry.Operation;
            this.Type = entry.Type;
        }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        [PrimaryKey, AutoIncrement, Column("id"), JsonProperty("id"), XmlElement("id")]
        public int Id
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the operation.
        /// </summary>
        /// <value>The operation.</value>
        [Column("operation"), NotNull, JsonProperty("operation"), XmlElement("operation")]
        public SynchronizationOperationType Operation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the model type
        /// </summary>
        /// <value>The type.</value>
        [Column("type"), JsonProperty("type"), XmlElement("type")]
        public String Type
        {
            get;
            set;
        }

        /// <summary>
        /// Creation time of the queue item
        /// </summary>
        /// <value>The creation time.</value>
        [Column("creation_time"), JsonProperty("creationTime"), XmlElement("creationTime")]
        public DateTime CreationTime
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the serialized data which is to be sent to the service (XML)
        /// </summary>
        /// <value>The data.</value>
        [Column("data"), JsonProperty("data"), XmlIgnore]
        public String DataFileKey
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the actual data object
        /// </summary>
        [XmlIgnore, JsonIgnore, Ignore]
        public IdentifiedData Data {
            get
            {
                if (this.m_transientData == null)
                    this.m_transientData = ApplicationContext.Current.GetService<IQueueFileProvider>().GetQueueData(this.DataFileKey, System.Type.GetType(this.Type));
                return this.m_transientData;
            }
            set
            {
                this.m_transientData = value;
            }
        }

        /// <summary>
        /// Identifies whether the queue item is a retry
        /// </summary>
        [Column("is_retry"), JsonProperty("isRetry"), XmlElement("isRetry")]
        public bool IsRetry { get; set; }

    }

    /// <summary>
    /// Outbound synchronization queue entry.
    /// </summary>
    [Table("outbound_queue"), JsonObject(nameof(OutboundQueueEntry)), XmlType(nameof(OutboundQueueEntry), Namespace = "http://santedb.org/queue")]
    public class OutboundQueueEntry : SynchronizationQueueEntry
    {
        /// <summary>
        /// admin ctor
        /// </summary>
        public OutboundQueueEntry()
        {

        }

        /// <summary>
        /// Create a new admin queue entry from outbound queue entry
        /// </summary>
        public OutboundQueueEntry(DeadLetterQueueEntry retryEntry) : base(retryEntry)
        {

        }
        /// <summary>
        /// Indicates the fail count
        /// </summary>
        [JsonProperty("retryCount"), Column("retry"), XmlElement("retryCount")]
        public int RetryCount { get; set; }
    }

    /// <summary>
    /// Dead letter queue entry - Dead letters are queue items that could not be synchronized for some reason.
    /// </summary>
    [Table("deadletter_queue"), JsonObject(nameof(DeadLetterQueueEntry)), XmlType(nameof(DeadLetterQueueEntry), Namespace = "http://santedb.org/queue")]
    public class DeadLetterQueueEntry : SynchronizationQueueEntry, ISynchronizationQueueRetryEntry
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.SQLite.Synchronization.Model.DeadLetterQueueEntry"/> class.
        /// </summary>
        public DeadLetterQueueEntry()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.SQLite.Synchronization.Model.DeadLetterQueueEntry"/> class.
        /// </summary>
        /// <param name="fromEntry">From entry.</param>
        public DeadLetterQueueEntry(SynchronizationQueueEntry fromEntry, byte[] tagData)
        {
            if (fromEntry == null)
                throw new ArgumentNullException(nameof(fromEntry));

            this.OriginalQueue = fromEntry.GetType().GetTypeInfo().GetCustomAttribute<TableAttribute>().Name;
            this.DataFileKey = ApplicationContext.Current.GetService<IQueueFileProvider>().CopyQueueData(fromEntry.DataFileKey);
            this.CreationTime = DateTime.Now;
            this.Type = fromEntry.Type;
            this.TagData = tagData;
            this.Operation = fromEntry.Operation;
        }

        /// <summary>
        /// The original queue name to which the dead letter item belonged. This can be used for retry enqueuing 
        /// </summary>
        /// <value>The original queue.</value>
        [Column("original_queue"), JsonProperty("originalQueue"), XmlElement("originalQueue")]
        public string OriginalQueue
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets data related to why the data is in the dead-letter queue
        /// </summary>
        [Column("tag"), JsonProperty("tag"), XmlElement("tag")]
        public byte[] TagData { get; set; }
    }

    /// <summary>
    /// Inbound queue represents an object which was received from the server that needs to be inserted into the SanteDB mobile database
    /// </summary>
    [Table("inbound_queue"), JsonObject(nameof(InboundQueueEntry)), XmlType(nameof(InboundQueueEntry), Namespace = "http://santedb.org/queue")]
    public class InboundQueueEntry : SynchronizationQueueEntry
    {
        /// <summary>
        /// Inbound ctor
        /// </summary>
        public InboundQueueEntry()
        {

        }

        /// <summary>
        /// Create a new inbound queue entry from outbound queue entry
        /// </summary>
        public InboundQueueEntry(DeadLetterQueueEntry retryEntry) : base(retryEntry)
        {

        }
    }

    /// <summary>
    /// Queue which is used to store administrative events on the user
    /// </summary>
    [Table("admin_queue"), JsonObject(nameof(OutboundAdminQueueEntry)), XmlType(nameof(OutboundAdminQueueEntry), Namespace = "http://santedb.org/queue")]
    public class OutboundAdminQueueEntry : OutboundQueueEntry
    {
        /// <summary>
        /// admin ctor
        /// </summary>
        public OutboundAdminQueueEntry()
        {

        }

        /// <summary>
        /// Create a new admin queue entry from outbound queue entry
        /// </summary>
        public OutboundAdminQueueEntry(DeadLetterQueueEntry retryEntry) : base(retryEntry)
        {

        }
    }
}

