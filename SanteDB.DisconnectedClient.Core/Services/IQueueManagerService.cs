/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Synchronization;
using System;
using System.Collections.Generic;

namespace SanteDB.DisconnectedClient.Services
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

    /// <summary>
    /// Represents a queue manager service
    /// </summary>
    public interface IQueueManagerService  : IServiceImplementation
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

        /// <summary>
        /// Exhausts the starts the exhaustion of the queue
        /// </summary>
        void ExhaustOutboundQueues();

    }
}
