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
 * User: fyfej
 * Date: 2024-1-23
 */
using System;
using System.Collections.Specialized;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Synchronization events
    /// </summary>
    public class SynchronizationEventArgs : EventArgs
    {
        /// <summary>
        /// Date of objects from pull
        /// </summary>
        public DateTime FromDate { get; }

        /// <summary>
        /// True if the pull is the initial pull
        /// </summary>
        public bool IsInitial { get; }

        /// <summary>
        /// Gets the type that was pulled
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the filter of the type that was pulled
        /// </summary>
        public NameValueCollection Filter { get; }

        /// <summary>
        /// Count of records imported
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Synchronization type events
        /// </summary>
        public SynchronizationEventArgs(Type type, NameValueCollection filter, DateTime fromDate, int totalSync)
        {
            this.Type = type;
            this.Filter = filter;
            this.IsInitial = fromDate == default(DateTime);
            this.Count = totalSync;
        }

    }
}
