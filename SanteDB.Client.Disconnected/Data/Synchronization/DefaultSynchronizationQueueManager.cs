using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    public class DefaultSynchronizationQueueManager : ISynchronizationQueueManager
    {
        public string ServiceName => "Synchronization Queue Manager";

        public ISynchronizationQueue Get(string queueName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ISynchronizationQueue> GetAll(SynchronizationPattern queueType)
        {
            throw new NotImplementedException();
        }
    }
}
