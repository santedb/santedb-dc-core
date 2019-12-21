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
 * User: Justin Fyfe
 * Date: 2019-12-16
 */
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
