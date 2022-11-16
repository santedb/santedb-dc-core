using SanteDB.Core.Http;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
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
                Type = badEntry.Type,
                EndpointType = badEntry.EndpointType
            });
        }


    }
}
