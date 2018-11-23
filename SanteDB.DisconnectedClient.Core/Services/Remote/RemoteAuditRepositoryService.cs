using MARC.HI.EHRS.SVC.Auditing.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services.Remote
{
    /// <summary>
    /// Represents a remote audit repository service that usses the AMI to communicate audits
    /// </summary>
    public class RemoteAuditRepositoryService : AmiRepositoryBaseService, IAuditRepositoryService
    {
      
        // Get a tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteAuditRepositoryService));

        /// <summary>
        /// Find the specified audit data
        /// </summary>
        public IEnumerable<AuditData> Find(Expression<Func<AuditData, bool>> query)
        {
            this.GetCredentials();
            int tr = 0;
            return this.Find(query, 0, null, out tr);
        }

        /// <summary>
        /// Find the specified audits
        /// </summary>
        public IEnumerable<AuditData> Find(Expression<Func<AuditData, bool>> query, int offset, int? count, out int totalResults)
        {
            this.GetCredentials();
            return this.m_client.Query(query, offset, count, out totalResults).CollectionItem.OfType<AuditData>();
        }

        /// <summary>
        /// Get the specified audit
        /// </summary>
        public AuditData Get(object correlationKey)
        {
            this.GetCredentials();
            if (correlationKey is Guid || correlationKey is Guid?)
                return this.m_client.GetAudit((Guid)correlationKey);
            else if (correlationKey is String)
                return this.m_client.GetAudit(Guid.Parse(correlationKey.ToString()));
            else
                throw new ArgumentException("Improper type supplied", nameof(correlationKey));
        }

        /// <summary>
        /// Insert the specified audit
        /// </summary>
        public AuditData Insert(AuditData audit)
        {
            this.GetCredentials();
            this.m_client.SubmitAudit(new AuditSubmission(audit));
            return audit;
        }

       
    }
}
