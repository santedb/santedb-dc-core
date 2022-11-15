using RestSrvr.Attributes;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Model.Patch;
using SanteDB.Rest.AppService;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Rest
{
    /// <summary>
    /// Synchronized application service contract
    /// </summary>
    public interface ISynchronizedAppServiceContract : IAppServiceContract
    {

        #region Queue
        /// <summary>
        /// Get content of all the queues
        /// </summary>
        [Get("/Queue")]
        Dictionary<String, int> GetQueue();

        /// <summary>
        /// Gets the queue entries
        /// </summary>
        [Get("/Queue/{queueName}")]
        List<ISynchronizationQueueEntry> GetQueue(String queueName);

        /// <summary>
        /// Get the specific queue entry
        /// </summary>
        [Get("/Queue/{queueName}/{id}")]
        IdentifiedData GetQueueData(String queueName, int id);

        /// <summary>
        /// Gets the conflict data a patch representing the difference between the server version and the local version being 
        /// </summary>
        [Get("/Queue/dead/{id}/conflict")]
        Patch GetQueueConflict(int id);

        /// <summary>
        /// Force a retry on the conflicted queue item
        /// </summary>
        [Post("/Queue/dead/{id}/$retry")]
        void RetryQueueEntry(int id, ParameterCollection parameters);

        /// <summary>
        /// Perform a patch / resolution
        /// </summary>
        [RestInvoke("PATCH", "/Queue/dead/{id}")]
        IdentifiedData ResolveQueueConflict(int id, Patch resolution);

        /// <summary>
        /// Remove a queue item
        /// </summary>
        [Delete("/Queue/{queueName}/{id}")]
        void DeleteQueueItem(String queueName, int id);
        #endregion


        #region Synchronization
        /// <summary>
        /// Get synchronization logs
        /// </summary>
        /// <returns></returns>
        [Get("/Sync")]
        List<ISynchronizationLogEntry> GetSynchronizationLogs();

        /// <summary>
        /// Synchronize the system immediately
        /// </summary>
        [Post("/Sync/$retry")]
        void SynchronizeNow(ParameterCollection parameters);

        /// <summary>
        /// Reset the synchornization status
        /// </summary>
        [Post("/Sync/$reset")]
        void ResetSynchronizationStatus(ParameterCollection parameters);
        #endregion

    }
}
