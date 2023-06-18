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
 * Date: 2023-5-19
 */
using SanteDB.Core.Security.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Default Queue manager for synchronization with 4 queues: <c>in</c>, <c>out</c>, <c>admin</c> and <c>deadletter</c>
    /// </summary>
    public class DefaultSynchronizationQueueManager : ISynchronizationQueueManager
    {
        /// <summary>
        /// The name of the incoming queue
        /// </summary>
        public const string QueueName_Incoming = "in";
        /// <summary>
        /// The name of the outbox queue
        /// </summary>
        public const string QueueName_Outgoing = "out";
        /// <summary>
        /// The name of the administrative queue
        /// </summary>
        public const string QueueName_Admin = "admin";
        /// <summary>
        /// The name of the dead letter queue
        /// </summary>
        public const string QueueName_DeadLetter = "deadletter";

        private List<ISynchronizationQueue> _Queues;
        private readonly ISymmetricCryptographicProvider _SymmetricCryptographicProvider;

        /// <summary>
        /// Instantiates an instance of the class.
        /// </summary>
        public DefaultSynchronizationQueueManager(ISymmetricCryptographicProvider symmetricCryptographicProvider)
        {
            this._SymmetricCryptographicProvider = symmetricCryptographicProvider;
            _Queues = new List<ISynchronizationQueue>
            {
                OpenQueue<SynchronizationDeadLetterQueueEntry>(QueueName_DeadLetter, SynchronizationPattern.DeadLetter),
                OpenQueue<SynchronizationQueueEntry>(QueueName_Outgoing, SynchronizationPattern.LocalToUpstream),
                OpenQueue<SynchronizationQueueEntry>(QueueName_Admin, SynchronizationPattern.LocalToUpstream),
                OpenQueue<SynchronizationQueueEntry>(QueueName_Incoming, SynchronizationPattern.UpstreamToLocal)
            };

        }

        private ISynchronizationQueue OpenQueue<TEntry>(string queueName, SynchronizationPattern type) where TEntry : ISynchronizationQueueEntry, new()
        {
            var path = GetQueuePath(queueName);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return new SynchronizationQueue<TEntry>(queueName, type, path, this._SymmetricCryptographicProvider);
        }

        private string GetQueuePath(string queueName)
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

        /// <inheritdoc/>
        public string ServiceName => "Synchronization Queue Manager";

        /// <inheritdoc/>
        public ISynchronizationQueue Get(string queueName)
        {
            if (!string.IsNullOrEmpty(queueName))
            {
                return _Queues.FirstOrDefault(q => queueName.Equals(q.Name));
            }
            return null;
        }

        /// <inheritdoc/>
        public IEnumerable<ISynchronizationQueue> GetAll(SynchronizationPattern queueType)
        {
            return _Queues.Where(q => q.Type == queueType);
        }
    }
}
