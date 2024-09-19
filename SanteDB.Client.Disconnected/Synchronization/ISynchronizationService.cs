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
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Services;
using System;
using System.Collections.Specialized;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// A service which is responsible for executing subscriptions on events with the upstream 
    /// integration service
    /// </summary>
    public interface ISynchronizationService : IServiceImplementation
    {

        /// <summary>
        /// Fired when a pull has completed and imported data
        /// </summary>
        event EventHandler PullCompleted;

        /// <summary>
        /// Fired when the push has been completed
        /// </summary>
        event EventHandler PushCompleted;

        /// <summary>
        /// Get whether the service is syncing
        /// </summary>
        bool IsSynchronizing { get; }

        /// <summary>
        /// Perform a pull with the specified trigger
        /// </summary>
        void Pull(SubscriptionTriggerType trigger);

        /// <summary>
        /// Pull data from the remove server and place it on the inbound queue
        /// </summary>
        void Pull(Type modelType);

        /// <summary>
        /// Pull data from the remove server and place it on the inbound queue
        /// </summary>
        void Pull(Type modelType, NameValueCollection filter);

        /// <summary>
        /// Pull data from the remove server and place it on the inbound queue
        /// </summary>
        void Pull(Type modelType, NameValueCollection filter, bool always);

        /// <summary>
        /// Push data to the server
        /// </summary>
        void Push();

        /// <summary>
        /// Instructs the synchronization service to perform the necessary steps to ensure that 
        /// the specified object is included on subsequent pull requests
        /// </summary>
        /// <param name="modelType">The type of object being subscribed to</param>
        /// <param name="objectKey">The key of the object which is to be subsribed to</param>
        void SubscribeTo(Type modelType, Guid objectKey);
    }
}
