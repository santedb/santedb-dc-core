/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-6-28
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Synchronization
{
    /// <summary>
    /// Synchronization operation type.
    /// </summary>
    public enum SynchronizationOperationType
    {
        /// <summary>
        /// The operation represents an inbound entry (sync)
        /// </summary>
        Sync = 0,
        /// <summary>
        /// Operation represents an insert (create) only if not existing
        /// </summary>
        Insert = 1,
        /// <summary>
        /// Operation represents an update
        /// </summary>
        Update = 2,
        /// <summary>
        /// Operation represents an obsolete
        /// </summary>
        Obsolete = 3
    }
}
