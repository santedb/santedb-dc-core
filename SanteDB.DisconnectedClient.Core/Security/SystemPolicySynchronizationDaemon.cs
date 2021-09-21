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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using System;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents a system policy synchornization daemon
    /// </summary>
    public class SystemPolicySynchronizationDaemon : IDaemonService
    {
        // True if this system is running
        private bool m_isRunning = false;

        // Safe to stop
        private bool m_safeToStop = true;

        // Trace logging
        private Tracer m_tracer = Tracer.GetTracer(typeof(SystemPolicySynchronizationDaemon));

        /// <summary>
        /// True if this service is running
        /// </summary>
        public bool IsRunning => this.m_isRunning;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "System Policy Synchronization";

        /// <summary>
        /// Service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Service is started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Service is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Service has stopped
        /// </summary>
        public event EventHandler Stopped;



        /// <summary>
        /// Start this service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_isRunning = true;
            this.m_safeToStop = false;
            ApplicationServiceContext.Current.Stopping += (o, e) => this.m_safeToStop = true; // Only allow stopping when app context stops
            ApplicationServiceContext.Current.Started += (o, e) =>
            {
                var pollInterval = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SynchronizationConfigurationSection>().PollInterval;
                ApplicationServiceContext.Current.GetService<IJobManagerService>().AddJob(new SystemPolicySynchronizationJob(), pollInterval);
            };

            this.Started?.Invoke(this, EventArgs.Empty);
            return this.m_isRunning;
        }

        /// <summary>
        /// Stop this service
        /// </summary>
        public bool Stop()
        {
            if (!this.m_safeToStop)
                throw new InvalidOperationException("Cannot stop this service while application is still running");
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.m_isRunning = false;

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return !this.m_isRunning;
        }
    }
}
