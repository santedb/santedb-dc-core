using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Disconnected.Services
{
    /// <summary>
    /// Represents an audit dispatcher which uses the administrative queue for the dispatching of audits.
    /// </summary>
    /// <remarks>
    /// In order to reduce the number of audits which are sent to the central environment, a dispatcher is used. This allows 
    /// only audits relevant audits to be sent to the central server via <see cref="ISynchronizationQueueManager"/>
    /// </remarks>
    public class SynchronizationAuditDispatcher : IAuditDispatchService
    {
        private readonly ISynchronizationQueueManager m_synchronizationQueueManager;
        private readonly ConcurrentQueue<AuditEventData> m_auditEventQueue = new ConcurrentQueue<AuditEventData>();
        private const int AUDIT_SUBMISSION_SIZE = 20;
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
                    var auditSubmission = new AuditSubmission()
                    {
                        ProcessId = Process.GetCurrentProcess().Id,
                        SecurityDeviceId = this.m_deviceId
                    };

                    while (this.m_auditEventQueue.TryPeek(out var peekAudit))
                    {
                        auditSubmission.Audit.Add(peekAudit);
                    }

                    this.m_synchronizationQueueManager.GetAll(SynchronizationPattern.LocalToUpstream | SynchronizationPattern.LowPriority).FirstOrDefault().Enqueue(auditSubmission, SynchronizationQueueEntryOperation.Insert);
                
                }
            }

        }
    }
}
