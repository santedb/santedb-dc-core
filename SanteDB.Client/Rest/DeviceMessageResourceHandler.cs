using SanteDB.Client.Exceptions;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Mail;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Attributes;
using SanteDB.Rest.HDSI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Rest
{
    /// <summary>
    /// Represents a specialized device resource handler which interacts with the upstream service to fetch messages as the 
    /// authenticated device
    /// </summary>
    public class DeviceMessageResourceHandler : IApiResourceHandler
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(DeviceMessageResourceHandler));
        private readonly IRepositoryService<Mailbox> m_mailboxRepository;
        private readonly IRepositoryService<MailboxMailMessage> m_mailMessageRepository;
        private readonly Guid m_deviceUuid;
        private Guid? m_deviceMailbox;


        /// <summary>
        /// DI ctor
        /// </summary>
        public DeviceMessageResourceHandler(IRepositoryService<Mailbox> mailboxRepository, 
            IRepositoryService<MailboxMailMessage> mailMessageRepository,
            IConfigurationManager configurationManager)
        {
            this.m_mailboxRepository = mailboxRepository;
            this.m_mailMessageRepository = mailMessageRepository;
            this.m_deviceUuid = configurationManager.GetSection<SecurityConfigurationSection>().GetSecurityPolicy(Core.Configuration.SecurityPolicyIdentification.AssignedDeviceSecurityId, Guid.Empty);
        }

        /// <inheritdoc/>
        public Type Type => typeof(MailboxMailMessage);

        /// <inheritdoc/>
        public ResourceCapabilityType Capabilities => ResourceCapabilityType.Search | ResourceCapabilityType.Get | ResourceCapabilityType.Delete;

        /// <inheritdoc/>
        public string ResourceName => $"Device{typeof(Mailbox).GetSerializationName()}";

        /// <inheritdoc/>
        public Type Scope => typeof(IHdsiServiceContract);

        private Guid GetDeviceMailbox()
        {
            if(this.m_deviceMailbox.HasValue)
            {
                return this.m_deviceMailbox.Value;
            }
            else if (this.m_deviceUuid != Guid.Empty)
            {
                this.m_deviceMailbox = this.m_mailboxRepository.Find(o => o.Name == Mailbox.INBOX_NAME && o.OwnerKey == this.m_deviceUuid).Select(o => o.Key).FirstOrDefault();
            }
            else if(AuthenticationContext.Current.GetDeviceIdentity() != null) // Is a device principal
            {
                var deviceName = AuthenticationContext.Current.GetDeviceIdentity().Name;
                this.m_deviceMailbox = this.m_mailboxRepository.Find(o => o.Name == Mailbox.INBOX_NAME && (o.Owner as SecurityDevice).Name == deviceName).Select(o => o.Key).FirstOrDefault();
            }
            else
            {
                throw new InvalidOperationException(ErrorMessages.PRINCIPAL_NOT_APPROPRIATE);
            }
            return this.m_deviceMailbox.GetValueOrDefault();
        }

        /// <inheritdoc/>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public object Get(object key, object versionKey)
        {
            if(key is Guid uuid)
            {
                // get the mailbox and then the message
                // We get the mail and then the 
                var deviceInbox = this.GetDeviceMailbox();
                var retVal= this.m_mailMessageRepository.Find(o => o.SourceEntityKey == deviceInbox && o.TargetEntityKey == uuid).FirstOrDefault();
                if (retVal == null)
                {
                    throw new KeyNotFoundException();
                }
                return retVal;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(key), String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(Guid), key.GetType()));
            }
        }

        /// <inheritdoc/>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public IQueryResultSet Query(NameValueCollection filter)
        {
            var sourceMailbox = this.GetDeviceMailbox();
            filter.Add("source", sourceMailbox.ToString());
            var whereClause = QueryExpressionParser.BuildLinqExpression<MailboxMailMessage>(filter);
            return this.m_mailMessageRepository.Find(whereClause);
        }

        /// <inheritdoc/>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public object Delete(object key)
        {
            if(key is Guid uuid)
            {
                var deviceInbox = this.GetDeviceMailbox();
                var retVal= this.m_mailMessageRepository.Find(o => o.SourceEntityKey == deviceInbox && o.TargetEntityKey == uuid).FirstOrDefault();
                if(retVal == null)
                {
                    throw new KeyNotFoundException();
                }
                return this.m_mailMessageRepository.Delete(retVal.Key.Value);
            }
            else
            {
                throw new InvalidOperationException(ErrorMessages.PRINCIPAL_NOT_APPROPRIATE);
            }
        }

        /// <inheritdoc/>
        public object Create(object data, bool updateIfExists)
        {
            throw new NotSupportedException();
        }
        public object Update(object data)
        {
            throw new NotSupportedException();
        }
    }
}
