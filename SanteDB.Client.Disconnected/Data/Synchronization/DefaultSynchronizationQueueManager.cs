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
    /// <summary>
    /// Default Queue manager for synchronization with 4 queues: <c>in</c>, <c>out</c>, <c>admin</c> and <c>deadletter</c>
    /// </summary>
    public class DefaultSynchronizationQueueManager : ISynchronizationQueueManager
    {
        public const string QueueName_Incoming = "in";
        public const string QueueName_Outgoing = "out";
        public const string QueueName_Admin = "admin";
        public const string QueueName_DeadLetter = "deadletter";

        private List<ISynchronizationQueue> _Queues;

        /// <summary>
        /// Instantiates an instance of the class.
        /// </summary>
        public DefaultSynchronizationQueueManager()
        {
            _Queues = new List<ISynchronizationQueue>
            {
                OpenQueue<SynchronizationDeadLetterQueueEntry>(QueueName_DeadLetter, SynchronizationPattern.DeadLetter),
                OpenQueue<SynchronizationQueueEntry>(QueueName_Outgoing, SynchronizationPattern.LocalToUpstream),
                OpenQueue<SynchronizationQueueEntry>(QueueName_Admin, SynchronizationPattern.LocalToUpstream),
                OpenQueue<SynchronizationQueueEntry>(QueueName_Incoming, SynchronizationPattern.UpstreamToLocal)
            };

        }

        private static ISynchronizationQueue OpenQueue<TEntry>(string queueName, SynchronizationPattern type) where TEntry: ISynchronizationQueueEntry, new()
        {
            var path = GetQueuePath(queueName);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return new SynchronizationQueue<TEntry>(queueName, type, path);
        }

        private static string GetQueuePath(string queueName)
        {
            if (null == queueName)
            {
                throw new ArgumentNullException(nameof(queueName));
            }

            return Path.Combine(
                AppDomain.CurrentDomain.GetData("DataDirectory")?.ToString(),
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
