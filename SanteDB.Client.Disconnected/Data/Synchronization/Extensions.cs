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
using DynamicExpresso;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    internal static class Extensions
    {
        public static ISynchronizationDeadLetterQueueEntry Enqueue(this SynchronizationQueue<SynchronizationDeadLetterQueueEntry> queue, ISynchronizationQueue originalQueue, ISynchronizationQueueEntry badEntry)
        {
            return queue.Enqueue(new SynchronizationDeadLetterQueueEntry()
            {
                CreationTime = badEntry.CreationTime,
                Data = badEntry.Data,
                DataFileKey = badEntry.DataFileKey,
                Id = badEntry.Id,
                IsRetry = false,
                Operation = badEntry.Operation,
                OriginalQueue = originalQueue.Name,
                Type = badEntry.Type
            });
        }

        public static void CompleteQuery(this ISynchronizationLogService service, ISynchronizationLogQuery query)
        {
            if (null == query)
            {
                return;
            }

            service.CompleteQuery(query.ResourceType, query.Filter, query.QueryId);
        }

        /// <summary>
        /// Gets the first queue from the queue manager that has an <see cref="SynchronizationPattern.UpstreamToLocal"/> queue pattern.
        /// </summary>
        /// <param name="service">The queue manager to query.</param>
        /// <returns>The instance of <see cref="ISynchronizationQueue"/> or <c>default</c>.</returns>
        public static ISynchronizationQueue GetInboundQueue(this ISynchronizationQueueManager service)
            => service.GetAll(SynchronizationPattern.UpstreamToLocal)?.FirstOrDefault();

        /// <summary>
        /// Enqueue multiple <see cref="IdentifiedData"/> objects to a queue.
        /// </summary>
        /// <param name="queue">The queue to add the entities to.</param>
        /// <param name="entities">The entities to enqueue on the queue.</param>
        /// <param name="operation">The operation to enqueue the entities with.</param>
        public static void Enqueue(this ISynchronizationQueue queue, IEnumerable<IdentifiedData> entities, SynchronizationQueueEntryOperation operation)
        {
            foreach(var entity in entities)
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

                    dlqueue.Enqueue(queue, data);

                    return SynchronizationMessagePump.Handled;

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
