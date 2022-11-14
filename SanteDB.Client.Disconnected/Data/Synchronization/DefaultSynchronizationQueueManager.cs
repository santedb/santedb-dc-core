using Antlr.Runtime.Tree;
using Newtonsoft.Json;
using SanteDB.Core;
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services.Impl;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    public class DefaultSynchronizationQueueManager : ISynchronizationQueueManager
    {

        private List<ISynchronizationQueue> _Queues;

        public DefaultSynchronizationQueueManager()
        {
            _Queues = new List<ISynchronizationQueue>();


        }


        public string ServiceName => "Synchronization Queue Manager";

        public ISynchronizationQueue Get(string queueName)
        {
            if (!string.IsNullOrEmpty(queueName))
            {
                return _Queues.FirstOrDefault(q => queueName.Equals(q.Name));
            }
            return null;
        }

        public IEnumerable<ISynchronizationQueue> GetAll(SynchronizationPattern queueType)
        {
            return _Queues.Where(q => q.Type == queueType);
        }
    }
}
