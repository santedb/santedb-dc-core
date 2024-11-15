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
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Queue;
using SanteDB.Core.Services;
using System;
using System.Linq;

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
            if (!this.IsUpstreamConfigured || !this.IsUpstreamAvailable())
            {
                return;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var submission = new AuditSubmission()
                    {
                        Key = Guid.NewGuid()
                    };

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
