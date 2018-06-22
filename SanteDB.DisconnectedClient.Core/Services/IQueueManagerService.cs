using SanteDB.DisconnectedClient.Core.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services
{


    /// <summary>
    /// Queue has been exhausted
    /// </summary>
    public class QueueExhaustedEventArgs : EventArgs
    {
        /// <summary>
        /// The queue which has been exhausted
        /// </summary>
        public String Queue { get; private set; }

        /// <summary>
        /// Gets or sets the object keys
        /// </summary>
        public IEnumerable<Guid> ObjectKeys { get; private set; }

        /// <summary>
        /// Queue has been exhausted
        /// </summary>
        public QueueExhaustedEventArgs(String queueName, params Guid[] objectKeys)
        {
            this.Queue = queueName;
            this.ObjectKeys = objectKeys;
        }
    }

    /// <summary>
    /// Represents a queue manager service
    /// </summary>
    public interface IQueueManagerService
    {

        /// <summary>
        /// Represents the admin queue
        /// </summary>
        ISynchronizationQueue Admin { get; }

        /// <summary>
        /// Represents the outbound queue
        /// </summary>
        ISynchronizationQueue Outbound { get; }

        /// <summary>
        /// Represents the inbound queue
        /// </summary>
        ISynchronizationQueue Inbound { get; }

        /// <summary>
        /// Represents the inbound queue
        /// </summary>
        ISynchronizationQueue DeadLetter { get; }

        /// <summary>
        /// Gets whether the service is busy
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Queue has been exhuasted
        /// </summary>
        event EventHandler<QueueExhaustedEventArgs> QueueExhausted;

    }
}
