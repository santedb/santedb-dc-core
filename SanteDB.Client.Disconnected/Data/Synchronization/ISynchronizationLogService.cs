using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a synchronization log service
    /// </summary>
    public interface ISynchronizationLogService
    {
        /// <summary>
        /// Get the last time that the specified type was synchronized
        /// </summary>
        DateTime? GetLastTime(Type modelType, String filter = null);

        /// <summary>
        /// Get the last ETag of the type
        /// </summary>
        String GetLastEtag(Type modelType, String filter = null);

        /// <summary>
        /// Update the log entry 
        /// </summary>
        void Save(Type modelType, String filter, String eTag, DateTime? since);

        /// <summary>
        /// Get all log entries
        /// </summary>
        IEnumerable<ISynchronizationLogEntry> GetAll();

        /// <summary>
        /// Save the specified query data for later continuation
        /// </summary>
        void SaveQuery(Type modelType, String filter, Guid queryId, int offset);

        /// <summary>
        /// Mark the specified query as complete
        /// </summary>
        void CompleteQuery(Type modelType, String filter, Guid queryId);
        /// <summary>
        /// Mark the specified query as complete
        /// </summary>
        void CompleteQuery(string modelType, String filter, Guid queryId);

        /// <summary>
        /// Find the query data
        /// </summary>
        ISynchronizationLogQuery FindQueryData(Type modelType, String filter);

        /// <summary>
        /// Deletes the specified log entry
        /// </summary>
        void Delete(ISynchronizationLogEntry itm);
    }
}
