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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Security
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
        /// Polls the system policy information
        /// </summary>
        private void PollSystemPolicy(object parm)
        {
            if (!this.m_isRunning) return; // Stop execution
            this.m_tracer.TraceInfo("Will start system policy polling");

            try
            {

                var netService = ApplicationServiceContext.Current.GetService<INetworkInformationService>();
                var localPip = ApplicationServiceContext.Current.GetService<IOfflinePolicyInformationService>();
                var localRp = ApplicationServiceContext.Current.GetService<IOfflineRoleProviderService>();
                var securityRepository = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>();
                var amiPip = new AmiPolicyInformationService();

                AuthenticationContext.Current = new AuthenticationContext(AuthenticationContext.SystemPrincipal);
               
                // Synchronize the groups
                var roleSync = new String[] { "SYSTEM", "ANONYMOUS", "DEVICE", "SYNCHRONIZERS" };
                foreach (var rol in roleSync)
                {
                    var group = securityRepository.GetRole(rol);
                    if (group == null)
                    {
                        localRp.CreateRole(rol, AuthenticationContext.SystemPrincipal);
                        group = securityRepository.GetRole(rol);
                    }

                    var activePolicies = amiPip.GetActivePolicies(group);
                    // Create local policy if not exists
                    foreach (var pol in activePolicies)
                        if (localPip.GetPolicy(pol.Policy.Oid) == null)
                            localPip.CreatePolicy(pol.Policy, AuthenticationContext.SystemPrincipal);
                    
                    // Assign policies
                    foreach (var pgroup in activePolicies.GroupBy(o => o.Rule))
                        localPip.AddPolicies(group, pgroup.Key, AuthenticationContext.SystemPrincipal, pgroup.Select(o => o.Policy.Oid).ToArray());

                }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceWarning("Could not refresh system policies: {0}", e);
            }
            var pollInterval = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SynchronizationConfigurationSection>().PollInterval;
            ApplicationServiceContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(pollInterval, this.PollSystemPolicy, null);
        }

        /// <summary>
        /// Start this service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_isRunning = true;
            this.m_safeToStop = false;
            ApplicationServiceContext.Current.Stopping += (o, e) => this.m_safeToStop = true; // Only allow stopping when app context stops

            // Bind timer
            ApplicationServiceContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(this.PollSystemPolicy, null);

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
