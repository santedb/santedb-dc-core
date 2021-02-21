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
using SanteDB.Core.Services;
using System;

namespace SanteDB.DisconnectedClient.Services
{
    /// <summary>
    /// Represents a service which can initiate a remote synchronization
    /// </summary>
    public interface IPushNotificationService : IServiceImplementation
    {
        /// <summary>
        /// Push has been received
        /// </summary>
        event EventHandler<PushNotificationEventArgs> MessageReceived;

    }

    /// <summary>
    /// Push notification event args
    /// </summary>
    public class PushNotificationEventArgs : EventArgs
    {
        /// <summary>
        /// The unique identifier of the object which was updated
        /// </summary>
        public Guid Key { get; set; }

        /// <summary>
        /// The type of resource the push is about
        /// </summary>
        public String Resource { get; set; }

        /// <summary>
        /// The data related to the push if the notification has it
        /// </summary>
        public IdentifiedData Data { get; set; }

    }
}
