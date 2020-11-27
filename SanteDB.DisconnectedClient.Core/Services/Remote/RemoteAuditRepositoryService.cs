/*
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.Core.Auditing;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Represents a remote audit repository service that usses the AMI to communicate audits
    /// </summary>
    public class RemoteAuditRepositoryService : AmiRepositoryBaseService, IAuditRepositoryService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Remote Audit Submission Repository";

        // Get a tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteAuditRepositoryService));

        /// <summary>
        /// Find the specified audit data
        /// </summary>
        public IEnumerable<AuditData> Find(Expression<Func<AuditData, bool>> query)
        {
            
            int tr = 0;
            return this.Find(query, 0, null, out tr);
        }

        /// <summary>
        /// Find the specified audits
        /// </summary>
        public IEnumerable<AuditData> Find(Expression<Func<AuditData, bool>> query, int offset, int? count, out int totalResults, params ModelSort<AuditData>[] orderBy)
        {
            using (var client = this.GetClient())
                return client.Query(query, offset, count, out totalResults, orderBy: orderBy).CollectionItem.OfType<AuditData>();
        }

        /// <summary>
        /// Get the specified audit
        /// </summary>
        public AuditData Get(object correlationKey)
        {
            using (var client = this.GetClient())
            {
                if (correlationKey is Guid || correlationKey is Guid?)
                    return client.GetAudit((Guid)correlationKey);
                else if (correlationKey is String)
                    return client.GetAudit(Guid.Parse(correlationKey.ToString()));
                else
                    throw new ArgumentException("Improper type supplied", nameof(correlationKey));
            }
        }

        /// <summary>
        /// Insert the specified audit
        /// </summary>
        public AuditData Insert(AuditData audit)
        {
            using (var client = this.GetClient())
                client.SubmitAudit(new AuditSubmission(audit));
            return audit;
        }


    }
}
