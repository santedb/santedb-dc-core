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

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents the synchronization job
    /// </summary>
    public class SystemPolicySynchronizationJob : IJob
    {

        // SErvice tickle
        private bool m_tickleWasSent = false;

        // Get tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SystemPolicySynchronizationJob));
        private readonly INetworkInformationService m_networkInformationService;
        private readonly IOfflinePolicyInformationService m_offlinePip;
        private readonly IOfflineRoleProviderService m_offlineRps;
        private readonly ISecurityRepositoryService m_securityRepository;
        private readonly IJobStateManagerService m_jobStateManager;
        private readonly IDataPersistenceService<SecurityChallenge> m_securityChallenge;
        private readonly IAdministrationIntegrationService m_amiIntegrationService;
        private readonly ITickleService m_tickleService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public SystemPolicySynchronizationJob(INetworkInformationService networkInformationService,
            ITickleService tickleService,
            IAdministrationIntegrationService amiIntegrationService, 
            IOfflinePolicyInformationService offlinePip, 
            IOfflineRoleProviderService offlineRps, 
            ISecurityRepositoryService securityRepository, 
            IJobStateManagerService jobStateManager,
            IDataPersistenceService<SecurityChallenge> securityChallengeService = null)
        {
            this.m_networkInformationService = networkInformationService;
            this.m_offlinePip = offlinePip;
            this.m_offlineRps = offlineRps;
            this.m_securityRepository = securityRepository;
            this.m_jobStateManager = jobStateManager;
            this.m_securityChallenge = securityChallengeService;
            this.m_amiIntegrationService = amiIntegrationService;
            this.m_tickleService = tickleService;
        }
        /// <summary>
        /// Gets the unique identifier of this job
        /// </summary>
        public Guid Id => Guid.Parse("31C2586A-6DAE-4AFB-8CFB-BAE1F4F26C3F");


        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "System Policy Synchronization";

        /// <inheritdoc/>
        public string Description => "Synchronizes the security policies assigned to security roles from the central iCDR server";

        /// <summary>
        /// Can cancel
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Parameters
        /// </summary>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>();

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
                    this.m_jobStateManager.SetState(this, JobStateType.Running);
                    var amiPip = new AmiPolicyInformationService();

                    try
                    {
                        foreach (var itm in amiPip.GetPolicies())
                        {
                            this.m_offlinePip.CreatePolicy(itm, AuthenticationContext.SystemPrincipal);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceError("Error synchronizing system policies - {0}", ex);
                    }

                    var systemRoles = new String[] { "SYNCHRONIZERS", "ADMINISTRATORS", "ANONYMOUS", "DEVICE", "SYSTEM", "USERS", "CLINICAL_STAFF", "LOCAL_USERS" };

                    // Synchronize the groups
                    foreach (var rol in this.m_offlineRps.GetAllRoles().Union(systemRoles))
                    {
                        try
                        {
                            var group = this.m_securityRepository.GetRole(rol);
                            if (group == null)
                            {
                                this.m_offlineRps.CreateRole(rol, AuthenticationContext.SystemPrincipal);
                                group = this.m_securityRepository.GetRole(rol);
                            }

                            var activePolicies = amiPip.GetPolicies(group);
                            // Create local policy if not exists
                            foreach (var pol in activePolicies)
                                if (this.m_offlinePip.GetPolicy(pol.Policy.Oid) == null)
                                    this.m_offlinePip.CreatePolicy(pol.Policy, AuthenticationContext.SystemPrincipal);

                            // Clear policies
                            var localPol = this.m_offlinePip.GetPolicies(group);
                            // Remove policies which no longer are granted
                            var noLongerGrant = localPol.Where(o => !activePolicies.Any(a => a.Policy.Oid == o.Policy.Oid));
                            this.m_offlinePip.RemovePolicies(group, AuthenticationContext.SystemPrincipal, noLongerGrant.Select(o => o.Policy.Oid).ToArray());
                            // Assign policies
                            foreach (var pgroup in activePolicies.GroupBy(o => o.Rule))
                                this.m_offlinePip.AddPolicies(group, pgroup.Key, AuthenticationContext.SystemPrincipal, pgroup.Select(o => o.Policy.Oid).ToArray());

                        }
                        catch (Exception)
                        {
                            this.m_tracer.TraceWarning("Could not sync {rol}");
                        }
                    }

                    // Query for challenges
                    if (this.m_securityChallenge != null)
                    {
                        var challenges = this.m_amiIntegrationService.Find<SecurityChallenge>(o => o.ObsoletionTime == null, 0, 10);
                        if (challenges != null)
                            foreach (var itm in challenges.Item.OfType<SecurityChallenge>())
                                if (this.m_securityChallenge.Get(itm.Key.Value, null, true, AuthenticationContext.SystemPrincipal) == null)
                                    this.m_securityChallenge.Insert(itm, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                    }

                    if (this.m_tickleWasSent) // a previous tickle was sent - let's notify the user that the sync is working again
                    {
                        this.m_tickleWasSent = false;
                        this.m_tickleService.SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Information, Strings.locale_syncRestored));
                    }
                    this.m_jobStateManager.SetState(this, JobStateType.Completed);
                }
                catch (Exception ex)
                {
                    this.m_jobStateManager.SetState(this, JobStateType.Aborted);
                    this.m_jobStateManager.SetProgress(this, ex.Message, 0.0f);
                    if (!this.m_tickleWasSent)
                    {
                        this.m_tickleService.SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format($"{Strings.locale_downloadError}: {Strings.locale_downloadErrorBody}", "Security Policy")));
                        this.m_tickleWasSent = true;
                    }
                    this.m_tracer.TraceWarning("Could not refresh system policies: {0}", ex);

                }
            }
        }
    }
}
