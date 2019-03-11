/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using SanteDB.Core.Mail;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Core.Services.Remote
{
    /// <summary>
    /// Represents a mail repository that connects to the AMI
    /// </summary>
    public class RemoteMailRepositoryService : AmiRepositoryBaseService, IMailMessageRepositoryService, IRepositoryService<MailMessage>
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Remote Mail Repository";

        /// <summary>
        /// Fired when a mail message was committed to the server
        /// </summary>
        public event EventHandler<MailMessageEventArgs> Committed;

        /// <summary>
        /// Fired when the mail message was received
        /// </summary>
        public event EventHandler<MailMessageEventArgs> Received;

        /// <summary>
        /// A mail message is to be broadcast locally
        /// </summary>
        public void Broadcast(MailMessage message)
        {
            this.Received?.Invoke(this, new MailMessageEventArgs(message));
        }

        /// <summary>
        /// Find the specified mail messages
        /// </summary>
        public IEnumerable<MailMessage> Find(Expression<Func<MailMessage, bool>> predicate, int offset, int? count, out int totalCount, params ModelSort<MailMessage>[] orderBy)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            return this.m_client.Query<MailMessage>(predicate, offset, count, out totalCount).CollectionItem.OfType<MailMessage>();
        }

        /// <summary>
        /// Find the specified mail messages
        /// </summary>
        public IEnumerable<MailMessage> Find(Expression<Func<MailMessage, bool>> query)
        {
            int tr = 0;
            return this.Find(query, 0, null, out tr);
        }

        /// <summary>
        /// Get the specified mail message
        /// </summary>
        public MailMessage Get(Guid id)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            return this.m_client.GetMailMessage(id);
        }

        /// <summary>
        /// Get the specified mail message
        /// </summary>
        public MailMessage Get(Guid key, Guid versionKey)
        {
            return this.Get(key);
        }

        /// <summary>
        /// Save the specified mail message
        /// </summary>
        public MailMessage Insert(MailMessage message)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            return this.m_client.CreateMailMessage(message);
        }

        /// <summary>
        /// Delete the specified message
        /// </summary>
        public MailMessage Obsolete(Guid key)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            var message = this.Get(key);
            message.Flags = MailMessageFlags.Archived;
            return this.m_client.UpdateMailMessage(key, message);
        }

        /// <summary>
        /// Save the specified mail message
        /// </summary>
        public MailMessage Save(MailMessage message)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            return this.m_client.UpdateMailMessage(message.Key.Value, message);
        }
    }
}
