using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Disconnected.Data.Synchronization
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
        public IEnumerable<Guid> Objects { get; private set; }

        /// <summary>
        /// Queue has been exhausted
        /// </summary>
        public QueueExhaustedEventArgs(String queueName, params Guid[] objects)
        {
            this.Queue = queueName;
            this.Objects = objects;
        }
    }

}
