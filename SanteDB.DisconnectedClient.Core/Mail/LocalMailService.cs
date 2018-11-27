/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-7-31
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Mail;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Core.Mail
{
    /// <summary>
    /// Represents a local alerting service
    /// </summary>
    public class LocalMailService : IMailMessageRepositoryService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Local Mail Storage Service";

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(LocalMailService));

        public event EventHandler<MailMessageEventArgs> Committed;

        public event EventHandler<MailMessageEventArgs> Received;

        /// <summary>
        /// Broadcast alert
        /// </summary>
        public void Broadcast(MailMessage msg)
        {
            try
            {
                this.m_tracer.TraceVerbose("Broadcasting alert {0}", msg);

                // Broadcast alert
                // TODO: Fix this, this is bad
                var args = new MailMessageEventArgs(msg);
                this.Received?.Invoke(this, args);
                if (args.Ignore)
                    return;

                if (msg.Flags == MailMessageFlags.Transient)
                    ApplicationContext.Current.ShowToast(msg.Subject);
                else
                    this.Save(msg);

                // Committed
                this.Committed?.BeginInvoke(this, args, null, null);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error broadcasting alert: {0}", e);
            }
        }

        /// <summary>
        /// Get alerts matching
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public IEnumerable<MailMessage> Find(Expression<Func<MailMessage, bool>> predicate, int offset, int? count, out int totalCount)
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<MailMessage>>();
            if (persistenceService == null)
                throw new InvalidOperationException("Cannot find alert persistence service");

            return persistenceService.Query(predicate, offset, count, out totalCount, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Get an alert from the storage
        /// </summary>
        public MailMessage Get(Guid id)
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<MailMessage>>();
            if (persistenceService == null)
                throw new InvalidOperationException("Cannot find alert persistence service");
            return persistenceService.Get(id, null, false, AuthenticationContext.Current.Principal);

        }

        /// <summary>
        /// Inserts an alert message.
        /// </summary>
        /// <param name="message">The alert message to be inserted.</param>
        /// <returns>Returns the inserted alert.</returns>
        public MailMessage Insert(MailMessage message)
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<MailMessage>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException(string.Format("{0} not found", nameof(IDataPersistenceService<MailMessage>)));
            }

            MailMessage alert = persistenceService.Insert(message, TransactionMode.Commit, AuthenticationContext.Current.Principal);
            this.Received?.Invoke(this, new MailMessageEventArgs(alert));

            return alert;
        }

        /// <summary>
        /// Save the alert without notifying anyone
        /// </summary>
        public MailMessage Save(MailMessage alert)
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<MailMessage>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException(string.Format("{0} not found", nameof(IDataPersistenceService<MailMessage>)));
            }

            try
            {
                // Transient messages don't get saved
                if (alert.Flags.HasFlag(MailMessageFlags.Transient))
                {
                    return alert;
                }

                this.m_tracer.TraceVerbose("Saving alert {0}", alert);

                var existingAlert = this.Get(alert.Key ?? Guid.Empty);

                if (existingAlert == null)
                {
                    persistenceService.Insert(alert, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                }
                else
                {
                    persistenceService.Update(alert, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                }
                this.Committed?.Invoke(this, new MailMessageEventArgs(alert));
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error saving alert: {0}", e);
            }

            return alert;
        }
    }
}