/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using RestSrvr.Attributes;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Model.Patch;
using SanteDB.Rest.AppService;
using System;
using System.Collections.Generic;

namespace SanteDB.Client.Disconnected.Rest
{
    /// <summary>
    /// Synchronized application service contract
    /// </summary>
    public interface ISynchronizedAppServiceContract : IAppServiceContract
    {

        #region Queue
        /// <summary>
        /// Get content of all the queues
        /// </summary>
        [Get("/Queue")]
        Dictionary<String, int> GetQueue();

        /// <summary>
        /// Gets the queue entries
        /// </summary>
        [Get("/Queue/{queueName}")]
        AmiCollection GetQueue(String queueName);

        /// <summary>
        /// Get the specific queue entry
        /// </summary>
        [Get("/Queue/{queueName}/{id}")]
        IdentifiedData GetQueueData(String queueName, int id);

        /// <summary>
        /// Gets the conflict data a patch representing the difference between the server version and the local version being 
        /// </summary>
        [Get("/Queue/deadletter/$diff")]
        Patch GetQueueConflict(ParameterCollection parameters);

        /// <summary>
        /// Force a retry on the conflicted queue item
        /// </summary>
        [Post("/Queue/deadletter/$retry")]
        void RetryQueueEntry(ParameterCollection parameters);

        /// <summary>
        /// Perform a patch / resolution
        /// </summary>
        [RestInvoke("PATCH", "/Queue/deadletter/{id}")]
        IdentifiedData ResolveQueueConflict(int id, Patch resolution);

        /// <summary>
        /// Remove a queue item
        /// </summary>
        [Delete("/Queue/{queueName}/{id}")]
        void DeleteQueueItem(String queueName, int id);
        #endregion


        #region Synchronization

        /// <summary>
        /// Get synchronization logs
        /// </summary>
        /// <returns></returns>
        [Get("/Sync")]
        List<ISynchronizationLogEntry> GetSynchronizationLogs();

        /// <summary>
        /// Reset the synchornization status
        /// </summary>
        [Post("/Sync/$reset")]
        void ResetSynchronizationStatus();

        /// <summary>
        /// Reset the synchornization status
        /// </summary> 
        [Post("/Sync/{id}/$reset")]
        void ResetSynchronizationStatus(String id);
        #endregion

    }
}
