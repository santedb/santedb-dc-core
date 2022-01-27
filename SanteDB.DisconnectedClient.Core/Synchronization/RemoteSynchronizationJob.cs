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
 * Date: 2021-2-19
 */
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.DisconnectedClient.Configuration;
using System;
using System.Collections.Generic;

namespace SanteDB.DisconnectedClient.Synchronization
{
    /// <summary>
    /// Represents a synchronization service which can query the HDSI and place 
    /// entries onto the inbound queue
    /// </summary>
    public class RemoteSynchronizationJob : IJob
    {

        /// <summary>
        /// Gets the identifier of this job
        /// </summary>
        public Guid Id => Guid.Parse("E511689A-98E1-47CD-8933-6A8CEF8AE014");

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteSynchronizationJob));

        /// <summary>
        /// Get the name
        /// </summary>
        public string Name => "Remote Synchronization Job";

        /// <inheritdoc/>
        public string Description => "Executes this dCDR's subscription check logic to download new information from the central iCDR server";

        /// <summary>
        /// True if synchronization can be cancelled
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Gets the current state
        /// </summary>
        public JobStateType CurrentState { get; private set; }

        /// <summary>
        /// Gets the parameter types
        /// </summary>
        public IDictionary<string, Type> Parameters => null;

        /// <summary>
        /// Time last started
        /// </summary>
        public DateTime? LastStarted { get; private set; }

        /// <summary>
        /// Last time finished
        /// </summary>
        public DateTime? LastFinished { get; private set; }

        /// <summary>
        /// Cancel the specified job
        /// </summary>
        public void Cancel()
        {
        }

        /// <summary>
        /// Run the synchronization
        /// </summary>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {
                this.LastStarted = DateTime.Now;
                this.CurrentState = JobStateType.Running;
                ApplicationServiceContext.Current.GetService<RemoteSynchronizationService>().Pull(SynchronizationPullTriggerType.PeriodicPoll);
                this.CurrentState = JobStateType.Completed;
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error running sync job {0}", ex);
                this.CurrentState = JobStateType.Aborted;
            }
            finally
            {
                this.LastFinished = DateTime.Now;
            }
        }
    }
}
