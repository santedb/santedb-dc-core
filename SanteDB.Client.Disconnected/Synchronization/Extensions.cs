﻿/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    internal static class Extensions
    {

        public const string OUTBOUND_QUEUE_NAME = "out";
        public const string INBOUND_QUEUE_NAME = "in";
        public const string ADMIN_QUEUE_NAME = "admin";
        public const string DEADLETTER_QUEUE_NAME = "deadletter";


        /// <summary>
        /// Get the administrative queue for low priority messages
        /// </summary>
        public static ISynchronizationQueue GetAdminQueue(this ISynchronizationQueueManager service) =>
            service.Get(ADMIN_QUEUE_NAME);

        /// <summary>
        /// Gets the first queue from the queue manager that has an <see cref="SynchronizationPattern.LocalToUpstream"/> queue pattern.
        /// </summary>
        /// <param name="service">The queue manager to query.</param>
        /// <returns>The instance of <see cref="ISynchronizationQueue"/> or <c>default</c>.</returns>
        public static ISynchronizationQueue GetOutboundQueue(this ISynchronizationQueueManager service)
            => service.Get(OUTBOUND_QUEUE_NAME);

        /// <summary>
        /// Gets the first queue from the queue manager that has an <see cref="SynchronizationPattern.DeadLetter"/> queue pattern.
        /// </summary>
        /// <param name="service">The queue manager to query.</param>
        /// <returns>The instance of <see cref="ISynchronizationQueue"/> or <c>default</c>.</returns>
        public static ISynchronizationQueue GetDeadletter(this ISynchronizationQueueManager service)
            => service.Get(DEADLETTER_QUEUE_NAME);

        /// <summary>
        /// Gets the first queue from the queue manager that has an <see cref="SynchronizationPattern.UpstreamToLocal"/> queue pattern.
        /// </summary>
        /// <param name="service">The queue manager to query.</param>
        /// <returns>The instance of <see cref="ISynchronizationQueue"/> or <c>default</c>.</returns>
        public static ISynchronizationQueue GetInboundQueue(this ISynchronizationQueueManager service)
            => service.Get(INBOUND_QUEUE_NAME);

        /// <summary>
        /// Enqueue multiple <see cref="IdentifiedData"/> objects to a queue.
        /// </summary>
        /// <param name="queue">The queue to add the entities to.</param>
        /// <param name="entities">The entities to enqueue on the queue.</param>
        /// <param name="operation">The operation to enqueue the entities with.</param>
        public static void Enqueue(this ISynchronizationQueue queue, IEnumerable<IdentifiedData> entities, SynchronizationQueueEntryOperation operation)
        {
            foreach (var entity in entities)
            {
                queue.Enqueue(entity, operation);
            }
        }

        public static string GetFirstEtag(this IEnumerable<IdentifiedData> entities)
            => entities?.Select(e => e?.Tag)?.FirstOrDefault(e => !string.IsNullOrEmpty(e));


        public static int RunDefault(this SynchronizationMessagePump messagepump, ISynchronizationQueue queue, Func<ISynchronizationQueueEntry, bool> callback)
        {
            int count = 0;

            messagepump.Run(queue,
                data =>
                {
                    try
                    {
                        return callback(data);
                    }
                    finally
                    {
                        count++;
                    }
                },
                (data, ex) =>
                {
                    var dlqueue = messagepump.GetDeadLetterQueue();
                    if (null == dlqueue)
                    {
                        return SynchronizationMessagePump.Unhandled;
                    }

                    // If the exception indicates a connection error to the server - we don't want to dead-letter them - just leave them be in the queue
                    if (!ex.IsCommunicationException())
                    {
                        dlqueue.Enqueue(data, ex.ToString());
                        //data.Queue.Delete(data.Id);
                        return SynchronizationMessagePump.Handled;

                    }
                    else
                    {
                        return SynchronizationMessagePump.Abort; // Abort sending the next message - leave this message in the queue for the next iteration of the pump
                    }

                });

            return count;
        }

        public static Expression<Func<object, bool>> ConvertToObjectLambda(this LambdaExpression expression, Type castType)
        {
            var pobj = Expression.Parameter(typeof(object), "o");
            return Expression.Lambda<Func<object, bool>>(
                Expression.Invoke(expression, Expression.Convert(pobj, castType)),
                pobj
                );
        }
    }
}
