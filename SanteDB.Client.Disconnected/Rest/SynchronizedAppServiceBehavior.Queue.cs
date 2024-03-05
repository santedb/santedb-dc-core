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
using RestSrvr;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Disconnected.Rest.Model;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Model.Patch;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Rest.AppService;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SanteDB.Client.Disconnected.Rest
{
    /// <summary>
    /// Application service behavior for queue operations
    /// </summary>
    public partial class SynchronizedAppServiceBehavior : AppServiceBehavior
    {
        //Declared in AppServiceBehavior.cs
        //this.m_synchronizationQueueManager
        //this.m_synchronizationLogService

        /// <summary>
        /// The queue name for the dead queue that is used by <see cref="GetQueueConflict(int)"/>, <see cref="ResolveQueueConflict(int, Patch)"/> and <see cref="RetryQueueEntry(int, ParameterCollection)"/>.
        /// </summary>
        private const string DeadletterQueueName = "deadletter"; //TODO: Is this right or can it be defined on ISynchronizationQueueService somewhere?
        private const string IdParameterName = "id";

        /// <summary>
        /// Uses the <see cref="ILocalizationService"/> to create an error message with the <paramref name="queueName"/>.
        /// </summary>
        /// <param name="queueName">The name of the queue that was not found.</param>
        /// <returns>A localized error message to insert into an Exception.</returns>
        private string ErrorMessage_QueueNotFound(string queueName) => m_localizationService.GetString("error.queue.notfound", new { queueName }); //TODO: Ensure error message is created.
        /// <summary>
        /// Uses the <see cref="ILocalizationService"/> to create an error message with the <paramref name="id"/> of the entry that is not found.
        /// </summary>
        /// <param name="id">The entry id that was not found.</param>
        /// <returns>A localized error message to insert into an Exception.</returns>
        private string ErrorMessage_QueueEntryNotFound(int id) => m_localizationService.GetString("error.queueentry.notfound", new { id });

        /// <summary>
        /// Uses the <see cref="ILocalizationService"/> to create an error message for the missing <see cref="IPatchService"/>.
        /// </summary>
        /// <returns>A localized error message to insert into an Exception.</returns>
        private string ErrorMessage_PatchServiceNull() => m_localizationService.GetString(ErrorMessageStrings.MISSING_SERVICE, new { serviceName = nameof(IPatchService) });

        /// <summary>
        /// Uses the <see cref="IServiceManager"/> to create an instance of <see cref="IDataPersistenceService{TData}"/> for the <paramref name="type"/> of data.
        /// </summary>
        /// <param name="type">The type of data to get a data persistence service for.</param>
        /// <returns>The constructed non-generic version of <see cref="IDataPersistenceService"/>.</returns>
        private IDataPersistenceService GetDataPersistenceService(string type)
            => m_serviceManager.CreateInjected(typeof(IDataPersistenceService<>).MakeGenericType(Type.GetType(type))) as IDataPersistenceService;

        /// <summary>
        /// Gets a queue from the <see cref="m_synchronizationQueueManager"/> by the name of the queue.
        /// </summary>
        /// <param name="queueName">The name of the queue to get from the service.</param>
        /// <returns>The <see cref="ISynchronizationQueue"/> instance.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when there is no queue with the name in <paramref name="queueName"/>.</exception>
        [DebuggerHidden]
        private ISynchronizationQueue GetQueueByName(string queueName)
            => m_synchronizationQueueManager?.Get(queueName) ?? throw new KeyNotFoundException(ErrorMessage_QueueNotFound(queueName));

        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public void DeleteQueueItem(string queueName, int id)
        {
            GetQueueByName(queueName)?.Delete(id);
        }

        /// <summary>
        /// Internal shared work from <see cref="GetQueueConflict(int)"/> and <see cref="ResolveQueueConflict(int, Patch)"/>. Retrieves the queue, item, suitable <see cref="IDataPersistenceService"/>, and queue version and database version of the internal <see cref="IdentifiedData"/>.
        /// </summary>
        /// <param name="id">The entry identifier to retrieve from the queue.</param>
        /// <returns>A tuple containing both the queue and database version of the identified data, as well as the <see cref="IDataPersistenceService"/> and <see cref="ISynchronizationQueue"/> used in <see cref="ResolveQueueConflict(int, Patch)"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the queue or entry cannot be found.</exception>
        /// <exception cref="NotSupportedException">Thrown when there is no patch service.</exception>
        private (IdentifiedData queueVersion, IdentifiedData dbVersion, IDataPersistenceService service, ISynchronizationQueue deadLetterQueue) GetQueueConflictInternal(int id)
        {
            ThrowIfNoPatchService();

            var queue = GetQueueByName(DeadletterQueueName);

            var item = queue?.Get(id) ?? throw new KeyNotFoundException(ErrorMessage_QueueEntryNotFound(id));

            var queueversion = item.Data;

            if (null == queueversion?.Key)
            {
                throw new NotSupportedException(m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            var persistenceservice = GetDataPersistenceService(item.ResourceType);

            var dbversion = (persistenceservice.Get(queueversion.Key.Value) as IdentifiedData) ?? throw new KeyNotFoundException(m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { type = item.ResourceType, id = queueversion.Key }));

            return (queueversion, dbversion, persistenceservice, queue);
        }

        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.ReadClinicalData)]
        public Patch GetQueueConflict(ParameterCollection parameters)
        {
            if (parameters.TryGet(IdParameterName, out int id))
            {
                (var queueversion, var dbversion, _, _) = GetQueueConflictInternal(id);

                return m_patchService?.Diff(dbversion, queueversion);
            }
            else
            {
                throw new ArgumentNullException(IdParameterName);
            }
        }

        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public IdentifiedData ResolveQueueConflict(int id, Patch resolution)
        {
            (var queueversion, var dbversion, var persistenceservice, var queue) = GetQueueConflictInternal(id);

            var retval = m_patchService.Patch(resolution, dbversion, force: true);

            retval = persistenceservice.Update(retval) as IdentifiedData;

            queue.Delete(id);

            return retval;
        }

        [DebuggerHidden]
        private void ThrowIfNoPatchService()
        {
            if (null == m_patchService)
            {
                throw new NotSupportedException(ErrorMessage_PatchServiceNull());
            }
        }

        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.LoginAsService)]
        public Dictionary<string, int> GetQueue()
        {
            IEnumerable<ISynchronizationQueue> queues = null;
            if (Enum.TryParse<SynchronizationPattern>(RestOperationContext.Current.IncomingRequest.QueryString["type"], out var filter))
            {
                queues = m_synchronizationQueueManager?.GetAll(filter);
            }
            else
            {
                queues = m_synchronizationQueueManager?.GetAll(SynchronizationPattern.All);
            }
            return queues?.ToDictionary(k => k.Name, v => v.Count());
        }

        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.LoginAsService)]
        public AmiCollection GetQueue(string queueName)
        {
            var qs = RestOperationContext.Current.IncomingRequest.QueryString;

            var queryFilter = QueryExpressionParser.BuildLinqExpression<ISynchronizationQueueEntry>(qs);

            var results = GetQueueByName(queueName)?.Query(queryFilter);

            if (null == results)
            {
                return new AmiCollection();
            }

            var retVal = results.ApplyResultInstructions(qs, out var offset, out var totalCount).OfType<ISynchronizationQueueEntry>()
                .Select(o => o is ISynchronizationDeadLetterQueueEntry dl ? new SynchronizationQueueDeadLetterEntryInfo(dl) : new SynchronizationQueueEntryInfo(o))
                .ToList();

            return new AmiCollection(retVal, offset, totalCount);

        }

        /*
        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.ReadClinicalData)]
        public ISynchronizationQueueEntry GetQueueEntry(string queueName, int id)
        {
            return GetQueueByName(queueName)?.Get(id);
        }
        */

        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.ReadClinicalData)]
        public IdentifiedData GetQueueData(string queueName, int id)
        {
            return GetQueueByName(queueName)?.Get(id).Data;
        }



        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public void RetryQueueEntry(ParameterCollection parameters)
        {
            if (parameters.TryGet(IdParameterName, out int id))
            {
                var queue = GetQueueByName(DeadletterQueueName);
                var item = (queue?.Get(id) as ISynchronizationDeadLetterQueueEntry) ?? throw new KeyNotFoundException(ErrorMessage_QueueEntryNotFound(id));
                item.OriginalQueue?.Enqueue(item, "RETRY");
                queue.Delete(id); // remove from DL
            }
            else if (parameters.TryGet("all", out bool all) && all)
            {
                var queue = GetQueueByName(DeadletterQueueName);
                foreach (var item in queue.Query(o => true).OfType<ISynchronizationDeadLetterQueueEntry>())
                {
                    item.OriginalQueue.Enqueue(item);
                    queue.Delete(item.Id);
                }
            }
            else
            {
                throw new ArgumentNullException(IdParameterName);
            }
        }
    }
}
