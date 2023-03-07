using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Queue;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// An upstream audit repository
    /// </summary>
    internal class UpstreamAuditRepository : AmiUpstreamRepository<AuditEventData>, IRepositoryService<AuditEventData>, IDisposable
    {

        // AMI queue name
        private const string AmiQueueName = "sys.ami";

        // Dead letter queue
        private const string AmiDeadQueueName = "sys.ami.dead";

        // Background sender thread
        private readonly IDispatcherQueueManagerService m_dispatcherQueue;

        /// <inheritdoc/>
        public UpstreamAuditRepository(IDispatcherQueueManagerService dispatcherQueueManagerService, 
            ILocalizationService localizationService, 
            IDataCachingService cacheService, 
            IRestClientFactory restClientFactory, 
            IUpstreamManagementService upstreamManagementService, 
            IUpstreamAvailabilityProvider upstreamAvailability, 
            IUpstreamIntegrationService upstreamIntegrationService, 
            IAdhocCacheService adhocCacheService,
            IAuditDispatchService auditDispatchService = null) : base(localizationService, cacheService, restClientFactory, upstreamManagementService, upstreamAvailability, upstreamIntegrationService, adhocCacheService)
        {
            this.m_dispatcherQueue = dispatcherQueueManagerService;
            this.m_dispatcherQueue.Open(AmiQueueName);
            this.m_dispatcherQueue.Open(AmiDeadQueueName);
            this.m_dispatcherQueue.SubscribeTo(AmiQueueName, this.MonitorOutboundQueue);
        }

        /// <summary>
        /// Monitor the outbound queue
        /// </summary>
        private void MonitorOutboundQueue(DispatcherMessageEnqueuedInfo enqueuedInfo)
        {
            if(!this.IsUpstreamConfigured || !this.IsUpstreamAvailable())
            {
                return;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var submission = new AuditSubmission();

                    do
                    {
                        submission.Audit.Clear();
                        while ((this.m_dispatcherQueue.Dequeue(AmiQueueName)?.Body is AuditEventData audit) && submission.Audit.Count < 20)
                        {
                            submission.Audit.Add(audit);
                        }

                        if (submission.Audit.Any())
                        {
                            try
                            {
                                client.SubmitAudit(submission);
                            }
                            catch (Exception e)
                            {
                                this._Tracer.TraceError("Error dispatching audit: {0}", e);
                                submission.Audit.ForEach(audit => this.m_dispatcherQueue.Enqueue(AmiDeadQueueName, audit));
                            }
                        }
                        else
                        {
                            break;
                        }
                    } while (true);
                }
            }
            catch (Exception e)
            {
                this._Tracer.TraceError("Error establishing connection to remote server for dispatching audits: {0}", e);
            }
        }

        /// <summary>
        /// Insert the specified audit
        /// </summary>
        public override AuditEventData Insert(AuditEventData audit)
        {
            this.m_dispatcherQueue.Enqueue(AmiQueueName, audit);
            return audit;
        }


        /// <summary>
        /// Update the specified audit data
        /// </summary>
        public override AuditEventData Save(AuditEventData data)
        {
            this.m_dispatcherQueue.Enqueue(AmiQueueName, data);
            return data;
        }

        /// <summary>
        /// Obsolete the specified audit
        /// </summary>
        public override AuditEventData Delete(Guid key)
        {
            throw new NotImplementedException(this.LocalizationService.GetString(ErrorMessageStrings.NOT_PERMITTED));
        }

        /// <summary>
        /// Dispose of the object
        /// </summary>
        public void Dispose()
        {
            this.m_dispatcherQueue.UnSubscribe(AmiQueueName, this.MonitorOutboundQueue);
            this.m_dispatcherQueue.UnSubscribe(AmiDeadQueueName, this.MonitorOutboundQueue);
        }
    }
}
