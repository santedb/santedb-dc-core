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
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents the synchronization job
    /// </summary>
    public class SystemPolicySynchronizationJob : IJob
    {
        // SErvice tickle
        private bool m_serviceTickle = false;

        // Get tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SystemPolicySynchronizationJob));

        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "System Policy Synchronization";

        /// <summary>
        /// Can cancel
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Current state
        /// </summary>
        public JobStateType CurrentState { get; private set; }

        /// <summary>
        /// Parameters
        /// </summary>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>();

        /// <summary>
        /// Last time started
        /// </summary>
        public DateTime? LastStarted { get; private set; }

        /// <summary>
        /// Last time finished
        /// </summary>
        public DateTime? LastFinished { get; private set; }

        /// <summary>
        /// Cancel
        /// </summary>
        public void Cancel()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Run the job
        /// </summary>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                try
                {

                    this.CurrentState = JobStateType.Running;
                    this.LastStarted = DateTime.Now;
                    var netService = ApplicationServiceContext.Current.GetService<INetworkInformationService>();
                    var localPip = ApplicationServiceContext.Current.GetService<IOfflinePolicyInformationService>();
                    var localRp = ApplicationServiceContext.Current.GetService<IOfflineRoleProviderService>();
                    var securityRepository = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>();
                    var amiPip = new AmiPolicyInformationService();

                    try
                    {
                        foreach (var itm in amiPip.GetPolicies())
                        {
                            localPip.CreatePolicy(itm, AuthenticationContext.SystemPrincipal);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceError("Error synchronizing system policies - {0}", ex);
                    }

                    var systemRoles = new String[] { "SYNCHRONIZERS", "ADMINISTRATORS", "ANONYMOUS", "DEVICE", "SYSTEM", "USERS", "CLINICAL_STAFF", "LOCAL_USERS" };

                    // Synchronize the groups
                    foreach (var rol in localRp.GetAllRoles().Union(systemRoles))
                    {
                        try
                        {
                            var group = securityRepository.GetRole(rol);
                            if (group == null)
                            {
                                localRp.CreateRole(rol, AuthenticationContext.SystemPrincipal);
                                group = securityRepository.GetRole(rol);
                            }

                            var activePolicies = amiPip.GetPolicies(group);
                            // Create local policy if not exists
                            foreach (var pol in activePolicies)
                                if (localPip.GetPolicy(pol.Policy.Oid) == null)
                                    localPip.CreatePolicy(pol.Policy, AuthenticationContext.SystemPrincipal);

                            // Clear policies
                            var localPol = localPip.GetPolicies(group);
                            // Remove policies which no longer are granted
                            var noLongerGrant = localPol.Where(o => !activePolicies.Any(a => a.Policy.Oid == o.Policy.Oid));
                            localPip.RemovePolicies(group, AuthenticationContext.SystemPrincipal, noLongerGrant.Select(o => o.Policy.Oid).ToArray());
                            // Assign policies
                            foreach (var pgroup in activePolicies.GroupBy(o => o.Rule))
                                localPip.AddPolicies(group, pgroup.Key, AuthenticationContext.SystemPrincipal, pgroup.Select(o => o.Policy.Oid).ToArray());

                        }
                        catch (Exception ex)
                        {
                            this.m_tracer.TraceWarning("Could not sync {rol}");
                        }
                    }

                    // Query for challenges
                    var localScs = ApplicationServiceContext.Current.GetService<IDataPersistenceService<SecurityChallenge>>();
                    if (localScs != null)
                    {
                        var amiIntegrationService = ApplicationServiceContext.Current.GetService<IAdministrationIntegrationService>();
                        var challenges = amiIntegrationService.Find<SecurityChallenge>(o => o.ObsoletionTime == null, 0, 10);
                        if (challenges != null)
                            foreach (var itm in challenges.Item.OfType<SecurityChallenge>())
                                if (localScs.Get(itm.Key.Value, null, true, AuthenticationContext.SystemPrincipal) == null)
                                    localScs.Insert(itm, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                    }

                    if (this.m_serviceTickle)
                    {
                        this.m_serviceTickle = false;
                        ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Information, Strings.locale_syncRestored));
                    }
                    this.LastFinished = DateTime.Now;
                    this.CurrentState = JobStateType.Completed;

                }
                catch (Exception ex)
                {
                    this.CurrentState = JobStateType.Cancelled;
                    if (!this.m_serviceTickle)
                    {
                        ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format($"{Strings.locale_downloadError}: {Strings.locale_downloadErrorBody}", "Security Policy")));
                        this.m_serviceTickle = true;
                    }
                    this.m_tracer.TraceWarning("Could not refresh system policies: {0}", ex);

                }
            }
        }
    }
}
