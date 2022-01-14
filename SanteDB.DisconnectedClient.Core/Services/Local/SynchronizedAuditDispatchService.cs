/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
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
    /// Local auditing service
    /// </summary>
    /// TODO: Split the job off
    public class SynchronizedAuditDispatchService : IAuditDispatchService, IDaemonService, IJob
    {

        /// <summary>
        /// Get the id of this job
        /// </summary>
        public Guid Id => Guid.Parse("4D45ED2F-C67C-4714-9302-FB0B5B9C48F5");

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Local Audit Dispatch Service";

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

        private bool m_safeToStop = false;

        // Tracer class
        private Tracer m_tracer = Tracer.GetTracer(typeof(SynchronizedAuditDispatchService));

        // Audit queue
        private ConcurrentQueue<AuditData> m_auditQueue = new ConcurrentQueue<AuditData>();

        // Reset event
        private AutoResetEvent m_resetEvent = new AutoResetEvent(false);

        private SecurityConfigurationSection m_securityConfiguration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SecurityConfigurationSection>();

        /// <summary>
        ///  True if the service is running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "Audit Dispatch Job";

        /// <summary>
        /// Can cancel
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Current state
        /// </summary>
        public JobStateType CurrentState { get; private set; }

        /// <summary>
        /// Gets type of parmaeters
        /// </summary>
        public IDictionary<string, Type> Parameters => null;

        /// <summary>
        /// Last time started
        /// </summary>
        public DateTime? LastStarted { get; private set; }

        /// <summary>
        /// Last time finished
        /// </summary>
        public DateTime? LastFinished { get; private set; }

        public event EventHandler Started;
        public event EventHandler Starting;
        public event EventHandler Stopped;
        public event EventHandler Stopping;

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
        /// Start auditor service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_safeToStop = false;
            ApplicationContext.Current.Started += (o, e) =>
            {
                try
                {
                    this.m_tracer.TraceInfo("Binding to service events...");

                    AuditData securityAlertData = new AuditData(DateTime.Now, ActionType.Execute, OutcomeIndicator.Success, EventIdentifierType.SecurityAlert, AuditUtil.CreateAuditActionCode(EventTypeCodes.AuditLoggingStarted));
                    AuditUtil.SendAudit(securityAlertData);
                }
                catch (Exception ex)
                {
                    this.m_tracer.TraceError("Error starting up audit repository service: {0}", ex);
                }
            };
            ApplicationServiceContext.Current.Stopping += (o, e) => this.m_safeToStop = true;

            // Queue user work item for sending
            var jms = ApplicationContext.Current.GetService<IJobManagerService>();
            jms.AddJob(this, JobStartType.TimerOnly);
            jms.SetJobSchedule(this, new TimeSpan(0, 5, 0));

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Stopped 
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            // Audit tool should never stop!!!!!
            if (!this.m_safeToStop)
            {
                AuditData securityAlertData = new AuditData(DateTime.Now, ActionType.Execute, OutcomeIndicator.EpicFail, EventIdentifierType.SecurityAlert, AuditUtil.CreateAuditActionCode(EventTypeCodes.AuditLoggingStopped));
                AuditUtil.AddLocalDeviceActor(securityAlertData);
                AuditUtil.SendAudit(securityAlertData);
            }
            else
            {
                AuditData securityAlertData = new AuditData(DateTime.Now, ActionType.Execute, OutcomeIndicator.Success, EventIdentifierType.SecurityAlert, AuditUtil.CreateAuditActionCode(EventTypeCodes.AuditLoggingStopped));
                AuditUtil.AddLocalDeviceActor(securityAlertData);
                AuditUtil.SendAudit(securityAlertData);
            }


            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Run the IJob on a delay
        /// </summary>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {
                this.CurrentState = JobStateType.Running;
                this.LastStarted = DateTime.Now;

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
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error running audit dispatch: {0}", ex);
                this.CurrentState = JobStateType.Aborted;
            }
            finally
            {
                this.LastFinished = DateTime.Now;
            }


        }

        /// <summary>
        /// Cancel the job
        /// </summary>
        public void Cancel()
        {
        }
    }
}
