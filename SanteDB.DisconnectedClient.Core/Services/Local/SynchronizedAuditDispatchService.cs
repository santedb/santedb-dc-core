/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
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
 * Date: 2019-12-24
 */
using SanteDB.Core;
using SanteDB.Core.Auditing;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SanteDB.DisconnectedClient.Security.Audit
{
    /// <summary>
    /// Local auditing service
    /// </summary>
    public class SynchronizedAuditDispatchService : IAuditDispatchService, IDaemonService
    {
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
        private Queue<AuditData> m_auditQueue = new Queue<AuditData>();

        // Reset event
        private AutoResetEvent m_resetEvent = new AutoResetEvent(false);

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

            lock (this.m_auditQueue)
                this.m_auditQueue.Enqueue(audit);
            this.m_resetEvent.Set();
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

                    // Queue has been exhausted
                    ApplicationContext.Current.GetService<IQueueManagerService>().QueueExhausted += (so, se) =>
                    {
                        if (se.Objects.Count() > 0)
                            switch (se.Queue)
                            {
                                case "inbound":
                                    if (ApplicationContext.Current.GetService<IQueueManagerService>().Inbound.Count() == 0)
                                        AuditUtil.AuditDataAction(EventTypeCodes.Import, ActionType.Create, AuditableObjectLifecycle.Import, EventIdentifierType.Import, OutcomeIndicator.Success, null, se.Objects.ToArray());
                                    break;
                                case "outbound":
                                    if (ApplicationContext.Current.GetService<IQueueManagerService>().Outbound.Count() == 0)
                                        AuditUtil.AuditDataAction(EventTypeCodes.Export, ActionType.Execute, AuditableObjectLifecycle.Export, EventIdentifierType.Export, OutcomeIndicator.Success, null, se.Objects.ToArray());
                                    break;
                            }
                    };

                }
                catch (Exception ex)
                {
                    this.m_tracer.TraceError("Error starting up audit repository service: {0}", ex);
                }
            };
            ApplicationServiceContext.Current.Stopping += (o, e) => this.m_safeToStop = true;

            AuditSubmission sendAudit = new AuditSubmission();

            // Send audit
            Action<Object> timerQueue = null;
            timerQueue = o =>
            {
                lock (sendAudit)
                    if (sendAudit.Audit.Count > 0)
                    {
                        ApplicationContext.Current.GetService<IQueueManagerService>().Admin.Enqueue(new AuditSubmission() { Audit = new List<AuditData>(sendAudit.Audit) }, SynchronizationOperationType.Insert);
                        sendAudit.Audit.Clear();
                    }
                ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(new TimeSpan(0, 0, 30), timerQueue, null);
            };

            // Queue user work item for sending
            ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(new TimeSpan(0, 0, 30), timerQueue, null);

            // Queue pooled item that monitors the audit queue and places them onto the outbound queue for a batch submission
            ApplicationContext.Current.GetService<IThreadPoolService>().QueueNonPooledWorkItem(o =>
            {
                while (!this.m_safeToStop)
                {
                    try
                    {
                        this.m_resetEvent.WaitOne();
                        while (this.m_auditQueue.Count > 0)
                        {
                            AuditData ad = null;

                            lock (this.m_auditQueue)
                                ad = this.m_auditQueue.Dequeue();

                            try
                            {
                                lock (sendAudit)
                                    sendAudit.Audit.Add(ad);
                            }
                            catch (Exception e)
                            {
                                this.m_tracer.TraceError("!!SECURITY ALERT!! >> Error sending audit {0}: {1}", ad, e);
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("!!SECURITY ALERT!! >> Error polling audit task list {0}", e);
                    }
                }
            }, null);
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

    }
}
