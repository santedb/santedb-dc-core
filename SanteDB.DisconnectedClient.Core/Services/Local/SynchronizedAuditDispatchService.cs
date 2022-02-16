/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using SanteDB.Core;
using SanteDB.Core.Auditing;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Synchronization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SanteDB.DisconnectedClient.Security.Audit
{
    /// <summary>
    /// Dispatcher service which uses the administrative queue
    /// </summary>
    /// <remarks>This service exists to reduce load on the database - whenever a process audits to the dispatcher this 
    /// class will collect all audits in memory and (on a schedule) will dump them into the admin queue</remarks>
    public class SynchronizedAuditDispatchService : IAuditDispatchService, IJob, IDisposable
    {

        /// <summary>
        /// Get the id of this job
        /// </summary>
        public Guid Id => Guid.Parse("4D45ED2F-C67C-4714-9302-FB0B5B9C48F5");

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Audit Dispatch Service";

        /// <summary>
        /// Dummy identiifed wrapper
        /// </summary>
        private class DummyIdentifiedWrapper : IdentifiedData
        {
            /// <summary>
            /// Modified on
            /// </summary>
            public override DateTimeOffset ModifiedOn
            {
                get
                {
                    return DateTimeOffset.Now;
                }
            }
        }

        // Tracer class
        private Tracer m_tracer = Tracer.GetTracer(typeof(SynchronizedAuditDispatchService));

        // Audit queue
        private ConcurrentQueue<AuditData> m_auditQueue = new ConcurrentQueue<AuditData>();

        // Reset event
        private AutoResetEvent m_resetEvent = new AutoResetEvent(false);

        // Security configuration
        private readonly SecurityConfigurationSection m_securityConfiguration;
        private readonly IJobStateManagerService m_jobStateManager;
        private readonly IQueueManagerService m_queueManagerService;

        /// <summary>
        /// Synchronized dispatch service
        /// </summary>
        public SynchronizedAuditDispatchService(IConfigurationManager configurationManager, IJobStateManagerService jobStateManager, IJobManagerService scheduleManager, IThreadPoolService threadPool, IQueueManagerService queueManagerService)
        {
            this.m_securityConfiguration = configurationManager.GetSection<SecurityConfigurationSection>();
            this.m_jobStateManager = jobStateManager;
            this.m_queueManagerService = queueManagerService;

            if(!scheduleManager.GetJobSchedules(this).Any())
            {
                scheduleManager.SetJobSchedule(this, new TimeSpan(0, 5, 0));
            }

            threadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    AuditData securityAlertData = new AuditData(DateTime.Now, ActionType.Execute, OutcomeIndicator.Success, EventIdentifierType.SecurityAlert, AuditUtil.CreateAuditActionCode(EventTypeCodes.AuditLoggingStarted));
                    AuditUtil.AddLocalDeviceActor(securityAlertData);
                    AuditUtil.SendAudit(securityAlertData);
                }
                catch (Exception ex)
                {
                    this.m_tracer.TraceError("Error starting up audit repository service: {0}", ex);
                }
            });
        }

        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "Audit Dispatch Job";

        /// <inheritdoc/>
        public string Description => "Checks the administrative dCDR synchronization queue and dispatches those audits to the central iCDR";

        /// <summary>
        /// Can cancel
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Gets type of parmaeters
        /// </summary>
        public IDictionary<string, Type> Parameters => null;

        // Duplicate guard
        private Dictionary<Guid, DateTime> m_duplicateGuard = new Dictionary<Guid, DateTime>();

        /// <summary>
        /// Send an audit (which stores the audit locally in the audit file and then queues it for sending)
        /// </summary>
        public void SendAudit(AuditData audit)
        {
            // Check duplicate guard
            Guid objId = Guid.Empty;
            var queryObj = audit.AuditableObjects.FirstOrDefault(o => o.Role == AuditableObjectRole.Query);
            if (queryObj != null && Guid.TryParse(queryObj.QueryData, out objId))
            {
                // prevent duplicate sending
                DateTime lastAuditObj = default(DateTime);
                if (this.m_duplicateGuard.TryGetValue(objId, out lastAuditObj) && DateTime.Now.Subtract(lastAuditObj).TotalSeconds < 2)
                    return; // duplicate
                else
                    lock (this.m_duplicateGuard)
                    {
                        if (this.m_duplicateGuard.ContainsKey(objId))
                            this.m_duplicateGuard[objId] = DateTime.Now;
                        else
                            this.m_duplicateGuard.Add(objId, DateTime.Now);
                    }
            }
            this.m_auditQueue.Enqueue(audit);
        }

        /// <summary>
        /// Run the IJob on a delay
        /// </summary>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {
                this.m_jobStateManager.SetState(this, JobStateType.Running);

                AuditSubmission submission = new AuditSubmission(); // To reduce size only submit 2 at a time
                while (this.m_auditQueue.TryDequeue(out AuditData data))
                {
                    submission.Audit.Add(data); // Add to submission
                    if (submission.Audit.Count == 3)
                    {
                        ApplicationServiceContext.Current.GetService<IQueueManagerService>().Admin.Enqueue(submission, SynchronizationOperationType.Insert);
                        submission = new AuditSubmission();
                    }
                }

                this.m_jobStateManager.SetState(this, JobStateType.Completed);
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error running audit dispatch: {0}", ex);
                this.m_jobStateManager.SetState(this, JobStateType.Aborted);
            }


        }

        /// <summary>
        /// Cancel the job
        /// </summary>
        public void Cancel()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Dispose of the job
        /// </summary>
        public void Dispose()
        {
            AuditData securityAlertData = new AuditData(DateTime.Now, ActionType.Execute, OutcomeIndicator.Success, EventIdentifierType.SecurityAlert, AuditUtil.CreateAuditActionCode(EventTypeCodes.AuditLoggingStopped));
            AuditUtil.AddLocalDeviceActor(securityAlertData);
            AuditUtil.SendAudit(securityAlertData);
        }
    }
}
