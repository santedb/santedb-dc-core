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
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a single log entry in the synchronization log
    /// </summary>
    public class SynchronizationLogEntry : IdentifiedData, ISynchronizationLogEntry
    {
        /// <summary>
        /// The type of resource 
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// The last time that the object was synchronized
        /// </summary>
        public DateTime LastSync { get; set; }

        /// <summary>
        /// The last ETAg which was fetched
        /// </summary>
        public string LastETag { get; set; }

        /// <summary>
        /// The filter which was applied on the synchronization 
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// The endpoint which was used to fetch the synchronization
        /// </summary>
        public ServiceEndpointType Endpoint { get; set; }

        /// <inheritdoc/>
        public override DateTimeOffset ModifiedOn => LastSync;
    }
}
