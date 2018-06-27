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
 * User: fyfej
 * Date: 2017-9-1
 */
using SanteDB.Core.Alerting;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Diagnostics;
using SanteDB.DisconnectedClient.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Core.Alerting
{
    /// <summary>
    /// Represents a local alerting service
    /// </summary>
    public class LocalAlertService : IAlertRepositoryService
    {
        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(LocalAlertService));

        public event EventHandler<AlertEventArgs> Committed;

        public event EventHandler<AlertEventArgs> Received;

        /// <summary>
        /// Broadcast alert
        /// </summary>
        public void BroadcastAlert(AlertMessage msg)
        {
            try
            {
                this.m_tracer.TraceVerbose("Broadcasting alert {0}", msg);

                // Broadcast alert
                // TODO: Fix this, this is bad
                var args = new AlertEventArgs(msg);
                this.Received?.Invoke(this, args);
                if (args.Ignore)
                    return;

                if (msg.Flags == AlertMessageFlags.Transient)
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
        public IEnumerable<AlertMessage> Find(Expression<Func<AlertMessage, bool>> predicate, int offset, int? count, out int totalCount)
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<AlertMessage>>();
            if (persistenceService == null)
                throw new InvalidOperationException("Cannot find alert persistence service");

            return persistenceService.Query(predicate, offset, count, out totalCount, Guid.Empty);
        }

        /// <summary>
        /// Get an alert from the storage
        /// </summary>
        public AlertMessage Get(Guid id)
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<AlertMessage>>();
            if (persistenceService == null)
                throw new InvalidOperationException("Cannot find alert persistence service");
            return persistenceService.Get(id);

        }

        /// <summary>
        /// Inserts an alert message.
        /// </summary>
        /// <param name="message">The alert message to be inserted.</param>
        /// <returns>Returns the inserted alert.</returns>
        public AlertMessage Insert(AlertMessage message)
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<AlertMessage>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException(string.Format("{0} not found", nameof(IDataPersistenceService<AlertMessage>)));
            }

            AlertMessage alert = persistenceService.Insert(message);
            this.Received?.Invoke(this, new AlertEventArgs(alert));

            return alert;
        }

        /// <summary>
        /// Save the alert without notifying anyone
        /// </summary>
        public AlertMessage Save(AlertMessage alert)
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<AlertMessage>>();

            if (persistenceService == null)
            {
                throw new InvalidOperationException(string.Format("{0} not found", nameof(IDataPersistenceService<AlertMessage>)));
            }

            try
            {
                // Transient messages don't get saved
                if (alert.Flags.HasFlag(AlertMessageFlags.Transient))
                {
                    return alert;
                }

                this.m_tracer.TraceVerbose("Saving alert {0}", alert);

                var existingAlert = this.Get(alert.Key ?? Guid.Empty);

                if (existingAlert == null)
                {
                    persistenceService.Insert(alert);
                }
                else
                {
                    persistenceService.Update(alert);
                }
                this.Committed?.Invoke(this, new AlertEventArgs(alert));
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error saving alert: {0}", e);
            }

            return alert;
        }
    }
}