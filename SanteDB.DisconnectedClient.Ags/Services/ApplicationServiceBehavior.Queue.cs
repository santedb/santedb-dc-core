/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SanteDB.DisconnectedClient.Core.Synchronization;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Patch;
using SanteDB.Core;
using SanteDB.DisconnectedClient.Core.Services;
using RestSrvr;
using SanteDB.Rest.Common.Attributes;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.Exceptions;
using SanteDB.DisconnectedClient.i18n;
using System.Threading;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.Core.Model.Query;

namespace SanteDB.DisconnectedClient.Ags.Services
{
	/// <summary>
    /// Represents an application service behavior for queue entries
    /// </summary>
    public partial class ApplicationServiceBehavior
    {

        // Already downloading
        private bool m_isDownloading;

        /// <summary>
        /// Get synchronization logs
        /// </summary>
		[DemandAttribute(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public List<ISynchronizationLogEntry> GetSynchronizationLogs()
        {
            var syncService = ApplicationServiceContext.Current.GetService<ISynchronizationService>();
            return syncService.Log;

        }

        /// <summary>
        /// Get all queues data
        /// </summary>
        /// <returns></returns>
		[DemandAttribute(PermissionPolicyIdentifiers.LoginAsService)]
        public Dictionary<String, int> GetQueue()
        {
            var queueManager = ApplicationServiceContext.Current.GetService<IQueueManagerService>();

            Dictionary<String, int> retVal = new Dictionary<string, int>()
            {
                {  "admin", queueManager?.Admin.Count() ?? 0 },
                {  "inbound", queueManager?.Inbound.Count()  ?? 0},
                {  "outbound", queueManager?.Outbound.Count() ?? 0 },
                {  "dead", queueManager?.DeadLetter.Count() ?? 0 }
            };


            return retVal;
        }

        /// <summary>
        /// Gets the queue entries
        /// </summary>
		[DemandAttribute(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public List<ISynchronizationQueueEntry> GetQueue(String queueName)
        {
            var queueManager = ApplicationServiceContext.Current.GetService<IQueueManagerService>();
            int offset = Int32.Parse(RestOperationContext.Current.IncomingRequest.QueryString["_offset"] ?? "0"), 
				count = Int32.Parse(RestOperationContext.Current.IncomingRequest.QueryString["_count"] ?? "100"),
				total = 0;
			
			switch(queueName.ToLower())
            {
                case "admin":
                    return queueManager.Admin.GetAll(offset, count, out total).ToList();
                case "inbound":
                    return queueManager.Inbound.GetAll(offset, count, out total).ToList();
                case "outbound":
                    return queueManager.Outbound.GetAll(offset, count, out total).ToList();
                case "dead":
                    return queueManager.DeadLetter.GetAll(offset, count, out total).ToList();
                default:
                    throw new KeyNotFoundException($"error.queue.notfound.{queueName}");
            }
        }

		/// <summary>
        /// Get the specified queue entry
        /// </summary>
		private ISynchronizationQueueEntry GetQueueEntry(String queueName, int id)
        {
            var queueManager = ApplicationServiceContext.Current.GetService<IQueueManagerService>();

            // Get the specified queue data
            switch (queueName.ToLower())
            {
                case "admin":
                    return queueManager.Admin.Get(id);
                case "inbound":
                    return queueManager.Inbound.Get(id);
                case "outbound":
                    return queueManager.Outbound.Get(id);
                case "dead":
                    return queueManager.DeadLetter.Get(id);
                default:
                    throw new KeyNotFoundException($"error.queue.notfound.{queueName}");
            }

        }

		/// <summary>
        /// Get the queue data
        /// </summary>
		private IdentifiedData GetQueueData(ISynchronizationQueueEntry queueEntry)
        {
            var queueData = ApplicationServiceContext.Current.GetService<IQueueFileProvider>().GetQueueData(queueEntry?.DataFileKey, Type.GetType(queueEntry.Type));
            if (queueData == null)
                throw new KeyNotFoundException($"error.queueentry.notfound.{queueEntry.DataFileKey}");
            return queueData;
        }

        /// <summary>
        /// Get the specific queue entry
        /// </summary>
        [DemandAttribute(PermissionPolicyIdentifiers.ReadClinicalData)]
        public IdentifiedData GetQueueData(String queueName, int id)
        {
            var data = this.GetQueueEntry(queueName, id);
            // Now get the data
            if (data == null)
                throw new KeyNotFoundException($"error.queueentry.notfound.{id}");

            return this.GetQueueData(data);
        }

        /// <summary>
        /// Gets the conflict data a patch representing the difference between the server version and the local version being 
        /// </summary>
        [DemandAttribute(PermissionPolicyIdentifiers.ReadClinicalData)]
        public Patch GetConflict(int id)
        {
            var queueData = this.GetQueueEntry("dead", id);
            if (queueData == null)
                throw new KeyNotFoundException($"error.queueentry.notfound.{id}");
            var queueVersion = this.GetQueueData(queueData);
            if (queueVersion == null)
                throw new KeyNotFoundException($"error.queudata.notfound.{queueData.DataFileKey}");

            var idb = typeof(IReadOnlyCollection<>).MakeGenericType(Type.GetType(queueData.Type));
            var persistenceService = ApplicationServiceContext.Current.GetService(idb);
            var databaseVersion = persistenceService.GetType().GetMethod("Get", new Type[] { typeof(Guid) })?.Invoke(persistenceService, new object[] { queueVersion.Key.Value }) as IdentifiedData;
            if (databaseVersion == null)
                throw new KeyNotFoundException($"error.localdb.notfound.{queueVersion.Key}");

            // Now create a patch between the 
            return ApplicationServiceContext.Current.GetService<IPatchService>().Diff(databaseVersion, queueVersion);
        }

        /// <summary>
        /// Perform a patch / resolution
        /// </summary>
        [DemandAttribute(PermissionPolicyIdentifiers.WriteClinicalData)]
        public IdentifiedData ResolveConflict(int id, Patch resolution)
        {
            // Posts a resolution patch to the service which we will apply locally and place into the outbound queue
            var queueData = this.GetQueueEntry("dead", id);
            if (queueData == null)
                throw new KeyNotFoundException($"error.queueentry.notfound.{id}");
            var queueVersion = this.GetQueueData(queueData);
            if (queueVersion == null)
                throw new KeyNotFoundException($"error.queudata.notfound.{queueData.DataFileKey}");
            
            // Data type
            var idb = typeof(IReadOnlyCollection<>).MakeGenericType(Type.GetType(queueData.Type));
            var persistenceService = ApplicationServiceContext.Current.GetService(idb);
            var databaseVersion = persistenceService.GetType().GetMethod("Get", new Type[] { typeof(Guid) })?.Invoke(persistenceService, new object[] { queueVersion.Key.Value }) as IdentifiedData;
            if (databaseVersion == null)
                throw new KeyNotFoundException($"error.localdb.notfound.{queueVersion.Key}");

            // Apply the patch
            if (resolution.AppliesTo.Key != databaseVersion.Key || resolution.AppliesTo.Key != queueVersion.Key)
                throw new PatchException($"Key mistmatch. Patch is for {resolution.AppliesTo.Key} however target object is {databaseVersion.Key}");

            var retVal = ApplicationServiceContext.Current.GetService<IPatchService>().Patch(resolution, databaseVersion, true);
            retVal = persistenceService.GetType().GetMethod("Save", new Type[] { Type.GetType(queueData.Type) }).Invoke(persistenceService, new object[] { retVal }) as IdentifiedData;

            // Now remove the issue
            this.DeleteQueueItem("dead", id);

            // return the new version
            return retVal;
        }

        /// <summary>
        /// Remove a queue item
        /// </summary>
        [DemandAttribute(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public void DeleteQueueItem(String queueName, int id)
        {

            var qmgr = ApplicationServiceContext.Current.GetService<IQueueManagerService>();
            // determine queue and remove
            switch(queueName.ToLower())
            {
                case "admin":
                    qmgr.Admin.Delete(id);
                    break;
                case "inbound":
                    qmgr.Inbound.Delete(id);
                    break;
                case "outbound":
                    qmgr.Outbound.Delete(id);
                    break;
                case "dead":
                    qmgr.DeadLetter.Delete(id);
                    break;
                default:
                    throw new KeyNotFoundException($"error.queue.notfound{queueName}");
            }
        }

        /// <summary>
        /// Retry the queue item
        /// </summary>
        [DemandAttribute(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public void Retry(int id)
        {
            var queueItem = this.GetQueueEntry("dead", id) as ISynchronizationQueueRetryEntry;
            if (queueItem == null)
                throw new KeyNotFoundException($"error.queueentry.notfound.{id}");

            // Retry
            ApplicationServiceContext.Current.GetService<IQueueManagerService>().DeadLetter.Retry(queueItem);

        }

        /// <summary>
        /// Synchronize the information now
        /// </summary>
        [DemandAttribute(PermissionPolicyIdentifiers.LoginAsService)]
        public void SynchronizeNow()
        {
            var queueService = ApplicationServiceContext.Current.GetService<IQueueManagerService>();
            var syncService = ApplicationServiceContext.Current.GetService<ISynchronizationService>();

            if (queueService.IsBusy || syncService.IsSynchronizing || this.m_isDownloading)
                throw new InvalidOperationException(Strings.err_already_syncrhonizing);
            else
            {
                ManualResetEventSlim waitHandle = new ManualResetEventSlim(false);

                ApplicationContext.Current.SetProgress(Strings.locale_waitForOutbound, 0.1f);

                // Wait for outbound queue to finish
                EventHandler<QueueExhaustedEventArgs> exhaustCallback = (o, e) =>
                {
                    if (e.Queue == "outbound")
                        waitHandle.Set();
                };

                queueService.QueueExhausted += exhaustCallback;
                queueService.ExhaustOutboundQueues();
                waitHandle.Wait();
                queueService.QueueExhausted -= exhaustCallback;

                this.m_isDownloading = true;
                try
                {
                    ApplicationContext.Current.SetProgress(String.Format(Strings.locale_downloading, ""), 0);
                    var targets =ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SynchronizationConfigurationSection>().SynchronizationResources.Where(o => o.Triggers.HasFlag(SynchronizationPullTriggerType.Always) || o.Triggers.HasFlag(SynchronizationPullTriggerType.OnNetworkChange) || o.Triggers.HasFlag(SynchronizationPullTriggerType.PeriodicPoll)).ToList();
                    for (var i = 0; i < targets.Count(); i++)
                    {
                        var itm = targets[i];
                        ApplicationContext.Current.SetProgress(String.Format(Strings.locale_downloading, itm.ResourceType.Name), (float)i / targets.Count);

                        if (itm.Filters.Count > 0)
                            foreach (var f in itm.Filters)
                                syncService.Pull(itm.ResourceType, NameValueCollection.ParseQueryString(f), itm.Always);
                        else
                            ApplicationContext.Current.GetService<ISynchronizationService>().Pull(itm.ResourceType);
                    }
                }
                finally
                {
                    this.m_isDownloading = false;
                }
            }
        }

        /// <summary>
        /// Force re-sync of all queue
        /// </summary>
        [DemandAttribute(PermissionPolicyIdentifiers.LoginAsService)]
        public void RetryAll()
        {
            // Force resynchronization
            foreach (var itm in this.GetQueue("dead").OfType<ISynchronizationQueueRetryEntry>())
                ApplicationServiceContext.Current.GetService<IQueueManagerService>().DeadLetter.Retry(itm);

        }
    }
}
