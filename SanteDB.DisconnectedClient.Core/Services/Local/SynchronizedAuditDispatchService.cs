using SanteDB.Core;
using SanteDB.Core.Auditing;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Audit dispatch service that dispatches audits using the administrative queue
    /// </summary>
    public class SynchronizedAuditDispatchService : IAuditDispatchService
    {
        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Synchronized Audit Dispatch";

        /// <summary>
        /// Push audit data to the queue
        /// </summary>
        public void SendAudit(AuditData audit)
        {
            var submission = new AuditSubmission(audit);
            ApplicationServiceContext.Current.GetService<IQueueManagerService>().Admin.Enqueue(submission, Synchronization.SynchronizationOperationType.Insert);
        }
    }
}
