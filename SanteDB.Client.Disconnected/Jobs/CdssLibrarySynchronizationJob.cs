using Hl7.Fhir.Utility;
using SanteDB.Cdss.Xml;
using SanteDB.Cdss.Xml.Ami;
using SanteDB.Cdss.Xml.Model;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Http;
using SanteDB.Core.Cdss;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Disconnected.Jobs
{
    /// <summary>
    /// An implementation of an <see cref="IJob"/> which is responsible for the synchronization of upstream 
    /// <see cref="CdssLibraryDefinition"/> instances and coordinating with the <see cref="ISynchronizationLogService"/>
    /// to record the synchronized times.
    /// </summary>
    public class CdssLibrarySynchronizationJob : ISynchronizationJob
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(CdssLibrarySynchronizationJob));

        private readonly ICdssLibraryRepository m_cdssLibraryRepositoryService;
        private readonly IUpstreamIntegrationService m_upstreamIntegrationService;
        private readonly IUpstreamAvailabilityProvider m_upstreamAvailabilityProvider;
        private readonly IRestClientFactory m_restClientFactory;
        private readonly ISynchronizationLogService m_synchronizationLogService;
        private readonly IJobStateManagerService m_jobStateManagerService;

        /// <summary>
        /// The JOB identifier of this job
        /// </summary>
        public static readonly Guid JOB_ID = Guid.Parse("5D69C625-2D99-4EEF-BF33-C277B784CF78");

        /// <summary>
        /// DI constructor
        /// </summary>
        public CdssLibrarySynchronizationJob(ISynchronizationLogService synchronizationLogService, 
            IJobStateManagerService jobStateManagerService, 
            IUpstreamIntegrationService upstreamIntegrationService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IRestClientFactory restClientFactory,
            ICdssLibraryRepository cdssLibraryRepository)
        {
            this.m_synchronizationLogService = synchronizationLogService;
            this.m_jobStateManagerService = jobStateManagerService;
            this.m_cdssLibraryRepositoryService = cdssLibraryRepository;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
            this.m_upstreamAvailabilityProvider = upstreamAvailabilityProvider;
            this.m_restClientFactory = restClientFactory;
        }

        /// <inheritdoc/>
        public Guid Id => JOB_ID;

        /// <inheritdoc/>
        public string Name => "CDSS Library Synrhonization Job";

        /// <inheritdoc/>
        public string Description => "Synchronizes CDSS libraries from the iCDR and ensures that the local instance of decision support rules are up to date";

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
                if(!this.m_upstreamAvailabilityProvider.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    this.m_jobStateManagerService.SetState(this, JobStateType.Cancelled);
                    return;
                }

                this.m_jobStateManagerService.SetState(this, JobStateType.Running);

                using (AuthenticationContext.EnterSystemContext()) {
                    var synchronizationLog = this.m_synchronizationLogService.Get(typeof(CdssLibraryDefinition));

                    if (parameters?.Length == 1 && 
                        Boolean.TryParse(parameters[0]?.ToString(), out bool resyncAll) && resyncAll)
                    {
                        this.m_synchronizationLogService.Delete(synchronizationLog);
                        synchronizationLog = null;
                        // Remove all existing
                        foreach (var cdss in this.m_cdssLibraryRepositoryService.Find(o => true).ToArray()) {
                            this.m_cdssLibraryRepositoryService.Remove(cdss.Uuid);
                        }
                    }

                    if (synchronizationLog == null)
                    {
                        synchronizationLog = this.m_synchronizationLogService.Create(typeof(CdssLibraryDefinition));
                    }


                    this.m_tracer.TraceInfo("Will synchronize CDSS libraries modified since {0}", synchronizationLog.LastSync);

                    // Determine the last synchronization query 
                    using(var client = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                    {
                        client.Credentials = new UpstreamDeviceCredentials(this.m_upstreamIntegrationService.AuthenticateAsDevice());
                        string lastEtag = null;
                        if (synchronizationLog.LastSync.HasValue) {
                            client.Requesting += (o, ev) => ev.AdditionalHeaders.Add(System.Net.HttpRequestHeader.IfModifiedSince, synchronizationLog.LastSync.ToString());
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
                                this.m_cdssLibraryRepositoryService.InsertOrUpdate(new XmlProtocolLibrary(libraryData.Library));
                            }
                            this.m_synchronizationLogService.Save(synchronizationLog, lastEtag, DateTime.Now);
                        }
                    }

                }
                this.m_jobStateManagerService.SetState(this, JobStateType.Completed);
            }
            catch(Exception ex)
            {
                this.m_jobStateManagerService.SetState(this, JobStateType.Aborted, ex.ToHumanReadableString());
                this.m_jobStateManagerService.SetProgress(this, ex.Message, 0.0f);
                this.m_tracer.TraceError("Error executing job {0} - {1}", nameof(CdssLibrarySynchronizationJob), ex);
            }
        }
    }
}
