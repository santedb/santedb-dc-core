﻿/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using Newtonsoft.Json;
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SanteDB.Core.Mail;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient;
using SQLite.Net.Attributes;
using System;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.SQLite.Mail
{

    /// <summary>
    /// A message
    /// </summary>
    [JsonObject, Table("mailbox"), XmlType(nameof(MailMessage), Namespace = "http://santedb.org/mail")]
    public class DbMailMessage
    {

        public DbMailMessage()
        {
            this.Id = Guid.NewGuid().ToByteArray();
        }

        /// <summary>
        /// Creates a new alert message
        /// </summary>
        public DbMailMessage(MailMessage am)
        {

            this.TimeStamp = am.TimeStamp.DateTime;
            this.From = am.From;
            this.Subject = am.Subject;
            this.Body = am.Body;
            this.To = am.To;
            this.CreatedBy = AuthenticationContext.Current.Principal?.Identity.Name ?? "SYSTEM";
            this.Flags = (int)am.Flags;
            this.Id = am.Key.HasValue ? am.Key.Value.ToByteArray() : Guid.NewGuid().ToByteArray();
            this.CreationTime = DateTime.Now;
        }

        /// <summary>
        /// Gets the alert
        /// </summary>
        public MailMessage ToAlert()
        {
            return new MailMessage(this.From, this.To, this.Subject, this.Body, (MailMessageFlags)this.Flags)
            {
                Key = new Guid(this.Id),
                CreationTime = new DateTimeOffset(this.CreationTime),
                TimeStamp = new DateTimeOffset(this.TimeStamp.GetValueOrDefault())
            };
        }

        /// <summary>
        /// Identifier
        /// </summary>
        [Column("key"), PrimaryKey, MaxLength(16)]
        public byte[] Id { get; set; }

        /// <summary>
        /// The time that the message was created
        /// </summary>
        [Column("creationTime")]
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Gets or sets the time
        /// </summary>
        [Column("time")]
        public DateTime? TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the status of the alert
        /// </summary>
        [Column("flags"), Indexed]
        public int Flags { get; set; }

        /// <summary>
        /// The principal that created the message
        /// </summary>
        [Column("created_by")]
        public String CreatedBy { get; set; }

        /// <summary>
        /// Identifies the to
        /// </summary>
        [Column("addrTo")]
        public String To { get; set; }

        /// <summary>
        /// Gets or sets the "from" subject if it is a human based message
        /// </summary>
        [Column("addrFrom")]
        public string From { get; set; }

        /// <summary>
        /// Gets or sets the subject
        /// </summary>
        [Column("subject")]
        public string Subject { get; set; }

        /// <summary>
        /// Body of the message
        /// </summary>
        [Column("body")]
        public string Body { get; set; }

        /// <summary>
        /// The intended recipient of this message
        /// </summary>
        [Column("rcptTo")]
        public byte[] Recipient { get; set; }
    }

}