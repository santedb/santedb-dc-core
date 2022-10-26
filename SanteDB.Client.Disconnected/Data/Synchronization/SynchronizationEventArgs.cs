using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace SanteDB.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Synchronization events
    /// </summary>
    public class SynchronizationEventArgs : EventArgs
    {
        /// <summary>
        /// Date of objects from pull
        /// </summary>
        public DateTime FromDate { get; }

        /// <summary>
        /// True if the pull is the initial pull
        /// </summary>
        public bool IsInitial { get; }

        /// <summary>
        /// Gets the type that was pulled
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the filter of the type that was pulled
        /// </summary>
        public NameValueCollection Filter { get; }

        /// <summary>
        /// Count of records imported
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Synchronization type events
        /// </summary>
        public SynchronizationEventArgs(Type type, NameValueCollection filter, DateTime fromDate, int totalSync) 
        {
            this.Type = type;
            this.Filter = filter;
            this.IsInitial = fromDate == default(DateTime);
            this.Count = totalSync;
        }

    }
}
