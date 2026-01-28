/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using DocumentFormat.OpenXml.Wordprocessing;
using SanteDB.Cdss.Xml.Model;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Http;
using SanteDB.Core.Configuration;
using SanteDB.Core.Data.Quality.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Jobs;
using SanteDB.Core.Matching;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using SanteDB.Matcher.Definition;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Rest.OAuth.Configuration;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Disconnected.Jobs
{
    /// <summary>
    /// Represents a job which synchronized the dCDR configuration properties with selected properties from the central server
    /// </summary>
    public class ConfigurationSynchronizationJob : ISynchronizationJob
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(ConfigurationSynchronizationJob));

        /// <summary>
        /// Job identification
        /// </summary>
        public readonly Guid JOB_ID = Guid.Parse("F05A1071-4DA0-4696-88BA-C98B3C7FE283");
        private readonly IConfigurationManager m_configurationManager;
        private readonly IUpstreamManagementService m_upstreamManagementService;
        private readonly IUpstreamAvailabilityProvider m_upstreamAvailabilityProvider;
        private readonly IJobStateManagerService m_jobStateManagerService;
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IRecordMatchingConfigurationService m_matchingConfigurationService;
        private readonly ISynchronizationLogService m_synchronizationLogService;
        private readonly IUpstreamIntegrationService m_upstreamIntegrationService;

        /// <summary>
        /// DI ctor
        /// </summary>
        public ConfigurationSynchronizationJob(IConfigurationManager configurationManager, 
            IUpstreamManagementService upstreamManagementService, 
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService,
            IRestClientFactory restClientFactory,
            IJobStateManagerService jobStateManagerService,
            IRecordMatchingConfigurationService matchingConfigurationService,
            ISynchronizationLogService synchronizationLogService)
        {
            this.m_configurationManager = configurationManager;
            this.m_upstreamManagementService = upstreamManagementService;
            this.m_upstreamAvailabilityProvider = upstreamAvailabilityProvider;
            this.m_jobStateManagerService = jobStateManagerService;
            this.m_restClientFactory = restClientFactory;
            this.m_matchingConfigurationService = matchingConfigurationService;
            this.m_synchronizationLogService = synchronizationLogService;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
        }

        /// <inheritdoc/>
        public Guid Id => JOB_ID;

        /// <inheritdoc/>
        public string Name => "Upstream Configuration Synchronization";

        /// <inheritdoc/>
        public string Description => "Propogates key settings from the central iCDR server to this instance of the dCDR";

        /// <inheritdoc/>
        public bool CanCancel => false;

        /// <inheritdoc/>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>();
        
        /// <inheritdoc/>
        public void Cancel()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            if (this.m_upstreamManagementService?.IsConfigured() != true || this.m_upstreamAvailabilityProvider?.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService) != true)
            {
                this.m_tracer.TraceInfo("Job {0}: The upstream realm is not configured.", nameof(SecurityObjectSynchronizationJob));
                this.m_jobStateManagerService.SetState(this, JobStateType.Cancelled);
                return;
            }

            using (AuthenticationContext.EnterSystemContext())
            {
                try
                {
                    this.m_jobStateManagerService.SetState(this, JobStateType.Running);
                    SyncUpstreamMatchConfigurations();
                    SyncUpstreamConfigurationDisclosures();
                    this.m_jobStateManagerService.SetState(this, JobStateType.Completed);
                }
                catch(Exception ex)
                {
                    this.m_jobStateManagerService.SetState(this, JobStateType.Aborted, ex.ToHumanReadableString());
                    this.m_jobStateManagerService.SetProgress(this, ex.Message, 0.0f);
                    this.m_tracer.TraceWarning("Could not propogate iCDR configuration changes to this dCDR: {0}", ex.ToString());
                }
            }
        }

        /// <summary>
        /// Synchronize the upstream match configurations
        /// </summary>
        private void SyncUpstreamMatchConfigurations()
        {
            if (this.m_upstreamAvailabilityProvider.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                var lastSyncLog = this.m_synchronizationLogService.Get(typeof(IRecordMatchingConfiguration)) ?? this.m_synchronizationLogService.Create(typeof(IRecordMatchingConfiguration));
                this.m_tracer.TraceInfo("Will synchronize match configuration entries");

                using (var client = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    client.Credentials = new UpstreamDeviceCredentials(this.m_upstreamIntegrationService.AuthenticateAsDevice());
                    if(lastSyncLog.LastSync.HasValue)
                    {
                        client.Requesting += (o, e) => e.AdditionalHeaders.Add(System.Net.HttpRequestHeader.IfModifiedSince, lastSyncLog.LastSync.ToString());
                    }

                    string lastEtag = null;
                    client.Responded += (o, e) => lastEtag = e.ETag;

                    var updatedMatchConfiguration = client.Get<AmiCollection>("MatchConfiguration");
                    if(updatedMatchConfiguration != null)
                    {
                        foreach(var mc in updatedMatchConfiguration.CollectionItem.OfType<IRecordMatchingConfiguration>())
                        {
                            this.m_matchingConfigurationService.SaveConfiguration(mc);
                        }
                        this.m_synchronizationLogService.Save(lastSyncLog, lastEtag, DateTimeOffset.Now);
                    }
                }

            }
        }

        /// <summary>
        /// Gets the upstream security configuration policies that may have changed (allowing local users, mandating MFA, etc.)
        /// </summary>
        private void SyncUpstreamConfigurationDisclosures()
        {
            if (this.m_upstreamAvailabilityProvider.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                // Use an options
                using (var client = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                using (var amiServiceClient = new AmiServiceClient(client))
                {
                    var serviceOptions = amiServiceClient.Options();
                    var ignoreSettings = new List<String>(); // Settings that have already been consumed

                    this.m_configurationManager.Configuration.Sections.OfType<IDisclosedConfigurationSection>().ForEach(sec =>
                    {
                        sec.Injest(serviceOptions.Settings);
                        ignoreSettings.AddRange(sec.ForDisclosure().Select(o => o.Key));
                    });
                    
                    // Allow OAUTH client credentials to be authenticated with an authenticated user principal
                    var securitySettings = this.m_configurationManager.GetSection<SecurityConfigurationSection>();
                    this.m_configurationManager.GetSection<OAuthConfigurationSection>().AllowClientOnlyGrant = securitySettings.GetSecurityPolicy(SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts, false);
                    // Get the general configuration and set them 
                    var appSetting = this.m_configurationManager.GetSection<ApplicationServiceContextConfigurationSection>();
                    serviceOptions.Settings.Where(o => !o.Key.StartsWith("$") && !ignoreSettings.Contains(o.Key)).ForEach(o => appSetting.AddAppSetting(o.Key, o.Value));

                    this.m_configurationManager.SaveConfiguration(restart: false);
                }
            }
        }

    }
}
