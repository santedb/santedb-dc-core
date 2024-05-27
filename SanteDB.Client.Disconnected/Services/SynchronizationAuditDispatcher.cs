/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2024-1-23
 */
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace SanteDB.Client.Disconnected.Services
{
    /// <summary>
    /// Represents an audit dispatcher which uses the administrative queue for the dispatching of audits.
    /// </summary>
    /// <remarks>
    /// In order to reduce the number of audits which are sent to the central environment, a dispatcher is used. This allows 
    /// only audits relevant audits to be sent to the central server via <see cref="ISynchronizationQueueManager"/>
    /// </remarks>
    public class SynchronizationAuditDispatcher : IAuditDispatchService, IDisposable
    {
        private readonly ISynchronizationQueueManager m_synchronizationQueueManager;
        private readonly ConcurrentQueue<AuditEventData> m_auditEventQueue = new ConcurrentQueue<AuditEventData>();
        private const int AUDIT_SUBMISSION_SIZE = 10;
        private readonly object m_lockBox = new object();
        private readonly Guid m_deviceId;

        /// <summary>
        /// Synchronization queue 
        /// </summary>
        public SynchronizationAuditDispatcher(ISynchronizationQueueManager synchronizationQueueManager, IConfigurationManager configurationManager)
        {
            this.m_synchronizationQueueManager = synchronizationQueueManager;
            this.m_deviceId = configurationManager.GetSection<SecurityConfigurationSection>().GetSecurityPolicy(Core.Configuration.SecurityPolicyIdentification.AssignedDeviceSecurityId, Guid.Empty);

        }

        /// <inheritdoc/>
        public string ServiceName => "Synchronization Audit Dispatcher";

        /// <inheritdoc/>
        public void SendAudit(AuditEventData audit)
        {

            this.m_auditEventQueue.Enqueue(audit);
            lock (this.m_lockBox) // block other threads from detecting the same condition until we can de-queue them
            {
                if (this.m_auditEventQueue.Count > AUDIT_SUBMISSION_SIZE)
                {
                    this.SubmitAuditEvents();
                }
            }

        }

        /// <summary>
        /// Service is being disposed - so send the audits out
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Dispose()
        {
            this.SubmitAuditEvents();
        }

        /// <summary>
        /// Submit all audit events in <see cref="m_auditEventQueue"/>
        /// </summary>
        private void SubmitAuditEvents()
        {
            var auditSubmission = new AuditSubmission()
            {
                ProcessId = Process.GetCurrentProcess().Id,
                SecurityDeviceId = this.m_deviceId
            };
            while (this.m_auditEventQueue.TryDequeue(out var peekAudit))
            {
                auditSubmission.Audit.Add(peekAudit);
            }
            this.m_synchronizationQueueManager.GetAdminQueue().Enqueue(auditSubmission, SynchronizationQueueEntryOperation.Insert);
        }
    }
}
