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

namespace SanteDB.DisconnectedClient.Ags.Services
{
	/// <summary>
    /// Represents an application service behavior for queue entries
    /// </summary>
    public partial class ApplicationServiceBehavior
    {

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
            var queueData = ApplicationServiceContext.Current.GetService<IQueueFileProvider>().GetQueueData(queueEntry?.Data, Type.GetType(queueEntry.Type));
            if (queueData == null)
                throw new KeyNotFoundException($"error.queueentry.notfound.{queueEntry.Data}");
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
                throw new KeyNotFoundException($"error.queudata.notfound.{queueData.Data}");

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
                throw new KeyNotFoundException($"error.queudata.notfound.{queueData.Data}");
            
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
        public void Retry(int id)
        {
            var queueItem = this.GetQueueEntry("dead", id) as ISynchronizationQueueRetryEntry;
            if (queueItem == null)
                throw new KeyNotFoundException($"error.queueentry.notfound.{id}");

            // Retry
            ApplicationServiceContext.Current.GetService<IQueueManagerService>().DeadLetter.Retry(queueItem);

        }

        /// <summary>
        /// Force re-sync of all queue
        /// </summary>
        [DemandAttribute(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public void RetryAll()
        {
            // Force resynchronization
            foreach (var itm in this.GetQueue("dead").OfType<ISynchronizationQueueRetryEntry>())
                ApplicationServiceContext.Current.GetService<IQueueManagerService>().DeadLetter.Retry(itm);

        }
    }
}
