/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you
 * may not use this file except in compliance with the License. You may
 * obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 *
 * User: fyfej
 * Date: 2023-3-10
 */
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
