using SanteDB.Core.Mail;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services.Remote
{
    /// <summary>
    /// Represents a mail repository that connects to the AMI
    /// </summary>
    public class RemoteMailRepositoryService : AmiRepositoryBaseService, IMailMessageRepositoryService, IRepositoryService<MailMessage>
    {

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
        public IEnumerable<MailMessage> Find(Expression<Func<MailMessage, bool>> predicate, int offset, int? count, out int totalCount)
        {
            this.GetCredentials();
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
            this.GetCredentials();
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
            this.GetCredentials();
            return this.m_client.CreateMailMessage(message);
        }

        /// <summary>
        /// Delete the specified message
        /// </summary>
        public MailMessage Obsolete(Guid key)
        {
            this.GetCredentials();
            var message = this.Get(key);
            message.Flags = MailMessageFlags.Archived;
            return this.m_client.UpdateMailMessage(key, message);
        }

        /// <summary>
        /// Save the specified mail message
        /// </summary>
        public MailMessage Save(MailMessage message)
        {
            this.GetCredentials();
            return this.m_client.UpdateMailMessage(message.Key.Value, message);
        }
    }
}
