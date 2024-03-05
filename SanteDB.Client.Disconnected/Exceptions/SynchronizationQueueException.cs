using SanteDB.Client.Disconnected.Data.Synchronization;
using System;

namespace SanteDB.Client.Disconnected.Exceptions
{
    /// <summary>
    /// Represents a <see cref="Exception"/> caused by an interaction with q synchronization queue
    /// </summary>
    public class SynchronizationQueueException : Exception
    {

        /// <summary>
        /// Gets the queue that raised this exception
        /// </summary>
        public ISynchronizationQueue Queue { get; }

        /// <summary>
        /// Creates a new synchronization queue exception with the specified <paramref name="queue"/> and <paramref name="message"/>
        /// </summary>
        /// <param name="queue">The queue that is raising the exception</param>
        /// <param name="message">The message of the exception</param>
        public SynchronizationQueueException(ISynchronizationQueue queue, String message) : this(queue, message, null)
        {
        }

        /// <summary>
        /// Creates a new synchronization queue exception with a specified <paramref name="innerException"/>
        /// </summary>
        /// <param name="queue">The queue which is raising the exception</param>
        /// <param name="message">The message for the exception</param>
        /// <param name="innerException">The exception which caused this</param>
        public SynchronizationQueueException(ISynchronizationQueue queue, String message, Exception innerException) : base(message, innerException)
        {
            this.Queue = queue;
        }
    }
}
