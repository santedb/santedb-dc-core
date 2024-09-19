/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using SanteDB.Cdss.Xml;
using SanteDB.Cdss.Xml.Ami;
using SanteDB.Cdss.Xml.Model;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Http;
using SanteDB.Core.Cdss;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Data.Quality.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SanteDB.Client.Disconnected.Jobs
{
    /// <summary>
    /// An implementation of an <see cref="IJob"/> which is responsible for the synchronization of upstream 
    /// <see cref="CdssLibraryDefinition"/> instances and coordinating with the <see cref="ISynchronizationLogService"/>
    /// to record the synchronized times.
    /// </summary>
    public class XCdssLibrarySynchronizationJob : ISynchronizationJob
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(XCdssLibrarySynchronizationJob));

        private readonly ICdssLibraryRepository m_cdssLibraryRepositoryService;
        private readonly IUpstreamIntegrationService m_upstreamIntegrationService;
        private readonly IUpstreamAvailabilityProvider m_upstreamAvailabilityProvider;
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IDataQualityConfigurationProviderService m_dataQualityConfigurationProvider;
        private readonly ISynchronizationLogService m_synchronizationLogService;
        private readonly IJobStateManagerService m_jobStateManagerService;

        /// <summary>
        /// The JOB identifier of this job
        /// </summary>
        public static readonly Guid JOB_ID = Guid.Parse("5D69C625-2D99-4EEF-BF33-C277B784CF78");

        /// <summary>
        /// DI constructor
        /// </summary>
        public XCdssLibrarySynchronizationJob(ISynchronizationLogService synchronizationLogService,
            IJobStateManagerService jobStateManagerService,
            IUpstreamIntegrationService upstreamIntegrationService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IRestClientFactory restClientFactory,
            IDataQualityConfigurationProviderService dataQualityConfigurationProviderService,
            ICdssLibraryRepository cdssLibraryRepository)
        {
            this.m_synchronizationLogService = synchronizationLogService;
            this.m_jobStateManagerService = jobStateManagerService;
            this.m_cdssLibraryRepositoryService = cdssLibraryRepository;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
            this.m_upstreamAvailabilityProvider = upstreamAvailabilityProvider;
            this.m_restClientFactory = restClientFactory;
            this.m_dataQualityConfigurationProvider = dataQualityConfigurationProviderService;
        }

        /// <inheritdoc/>
        public Guid Id => JOB_ID;

        /// <inheritdoc/>
        public string Name => "CDSS Library & Data Quality Synchronization Job";

        /// <inheritdoc/>
        public string Description => "Synchronizes CDSS libraries and data quality rules from the iCDR and ensures that the local instance of decision support rules are up to date";

        /// <inheritdoc/>
        public bool CanCancel => false;

        /// <inheritdoc/>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>()
        {
            { "resetCdss", typeof(Boolean) }
        };

        /// <inheritdoc/>
        public void Cancel()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {
               
                if (!this.m_upstreamAvailabilityProvider.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    this.m_jobStateManagerService.SetState(this, JobStateType.Cancelled);
                    return;
                }

                this.m_jobStateManagerService.SetState(this, JobStateType.Running);

                using (AuthenticationContext.EnterSystemContext())
                {
                    ISynchronizationLogEntry cdssSyncLog = this.m_synchronizationLogService.Get(typeof(CdssLibraryDefinition)),
                        dqSyncLog = this.m_synchronizationLogService.Get(typeof(DataQualityRulesetConfiguration));

                    if (parameters?.Length == 1 &&
                        Boolean.TryParse(parameters[0]?.ToString(), out bool resyncAll) && resyncAll)
                    {
                        this.m_synchronizationLogService.Delete(cdssSyncLog);
                        this.m_synchronizationLogService.Delete(dqSyncLog);
                        cdssSyncLog = dqSyncLog = null;
                        // Remove all existing
                        foreach (var cdss in this.m_cdssLibraryRepositoryService.Find(o => true).ToArray())
                        {
                            this.m_cdssLibraryRepositoryService.Remove(cdss.Uuid);
                        }
                        // Remove all dq configuration
                        foreach(var dq in this.m_dataQualityConfigurationProvider.GetRuleSets().ToArray())
                        {
                            this.m_dataQualityConfigurationProvider.RemoveRuleSet(dq.Id);
                        }
                    }

                    cdssSyncLog = cdssSyncLog ?? this.m_synchronizationLogService.Create(typeof(CdssLibraryDefinition));
                    dqSyncLog = dqSyncLog ?? this.m_synchronizationLogService.Create(typeof(DataQualityRulesetConfiguration));


                    this.m_tracer.TraceInfo("Will synchronize CDSS libraries modified since {0}", cdssSyncLog.LastSync);

                    EventHandler<RestRequestEventArgs> cdssModifiedHeader = (o, ev) => ev.AdditionalHeaders.Add(System.Net.HttpRequestHeader.IfModifiedSince, cdssSyncLog.LastSync.ToString()),
                        dqModifiedHeader = (o, ev) => ev.AdditionalHeaders.Add(System.Net.HttpRequestHeader.IfModifiedSince, dqSyncLog.LastSync.ToString());

                    // Determine the last synchronization query 
                    using (var client = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                    {
                        client.Credentials = new UpstreamDeviceCredentials(this.m_upstreamIntegrationService.AuthenticateAsDevice());

                        string lastEtag = null;
                        if (cdssSyncLog.LastSync.HasValue)
                        {
                            client.Requesting += cdssModifiedHeader;
                        }
                        client.Responded += (o, ev) => lastEtag = ev.ETag;

                        // Get the list of modified queries 
                        var updatedLibraries = client.Get<AmiCollection>("CdssLibraryDefinition");

                        // Updated libraries only contains the metadata - so we want to gether them 
                        if (updatedLibraries != null)
                        {
                            foreach (var itm in updatedLibraries.CollectionItem.OfType<CdssLibraryDefinitionInfo>())
                            {
                                // fetch the libraries 
                                var libraryData = client.Get<CdssLibraryDefinitionInfo>($"CdssLibraryDefinition/{itm.Key}");
                                if (libraryData != null)
                                {
                                    this.m_cdssLibraryRepositoryService.InsertOrUpdate(new XmlProtocolLibrary(libraryData.Library));
                                }
                            }
                        }

                        // All deleted libraries 
                        if (cdssSyncLog.LastSync.HasValue)
                        {
                            foreach (var itm in client.Post<ParameterCollection, String[]>("CdssLibraryDefinition/$deletedObjects", new ParameterCollection(new Parameter("since", cdssSyncLog.LastSync.Value.DateTime))))
                            {
                                this.m_cdssLibraryRepositoryService.Remove(Guid.Parse(itm));
                            }
                        }
                        this.m_synchronizationLogService.Save(cdssSyncLog, lastEtag, DateTime.Now);


                        // Synchronize DQ rules library
                        client.Requesting -= cdssModifiedHeader;
                        if (dqSyncLog.LastSync.HasValue)
                        {
                            client.Requesting += dqModifiedHeader;
                        }
                        var updatedRules = client.Get<AmiCollection>("DataQualityRulesetConfiguration");
                        if (updatedRules != null)
                        {
                            foreach(var itm in updatedRules.CollectionItem.OfType<DataQualityRulesetConfiguration>())
                            {
                                m_dataQualityConfigurationProvider.SaveRuleSet(itm);
                            }

                        }

                        // All deleted libraries 
                        if (dqSyncLog.LastSync.HasValue)
                        {
                            foreach (var itm in client.Post<ParameterCollection, String[]>("DataQualityRulesetConfiguration/$deletedObjects", new ParameterCollection(new Parameter("since", dqSyncLog.LastSync.Value.DateTime))))
                            {
                                this.m_dataQualityConfigurationProvider.RemoveRuleSet(itm);
                            }
                        }
                        this.m_synchronizationLogService.Save(dqSyncLog, lastEtag, DateTime.Now);

                    }

                }
                this.m_jobStateManagerService.SetState(this, JobStateType.Completed);
            }
            catch (Exception ex)
            {
                this.m_jobStateManagerService.SetState(this, JobStateType.Aborted, ex.ToHumanReadableString());
                this.m_jobStateManagerService.SetProgress(this, ex.Message, 0.0f);
                this.m_tracer.TraceError("Error executing job {0} - {1}", nameof(XCdssLibrarySynchronizationJob), ex);
            }
        }
    }
}
