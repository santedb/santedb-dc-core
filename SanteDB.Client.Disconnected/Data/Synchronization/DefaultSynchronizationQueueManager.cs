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
        public const string QueueName_Incoming = "incoming";
        public const string QueueName_Outgoing = "outgoing";
        public const string QueueName_Admin = "admin";
        public const string QueueName_DeadLetter = "deadletter";

        private List<ISynchronizationQueue> _Queues;

        public DefaultSynchronizationQueueManager()
        {
            _Queues = new List<ISynchronizationQueue>
            {
                OpenQueue(QueueName_DeadLetter),
                OpenQueue(QueueName_Admin),
                OpenQueue(QueueName_Outgoing),
                OpenQueue(QueueName_Incoming)
            };

        }

        private static ISynchronizationQueue OpenQueue(string queueName)
        {
            var path = GetQueuePath(queueName);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            switch (queueName)
            {
                case QueueName_DeadLetter:
                    return new SynchronizationQueue<SynchronizationDeadLetterQueueEntry>(queueName, SynchronizationPattern.DeadLetter, path);
                case QueueName_Incoming:
                    return new SynchronizationQueue<SynchronizationQueueEntry>(queueName, SynchronizationPattern.UpstreamToLocal, path);
                default:
                    return new SynchronizationQueue<SynchronizationQueueEntry>(queueName, SynchronizationPattern.LocalToUpstream, path);
            }
        }

        private static string GetQueuePath(string queueName)
        {
            if (null == queueName)
            {
                throw new ArgumentNullException(nameof(queueName));
            }

            //TODO: Use a configuration provider to get the path location.
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "santedb", 
                ApplicationServiceContext.Current.ApplicationName ?? "default", 
                "synchronizationqueue", 
                queueName
            );
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
