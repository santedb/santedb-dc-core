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
using Newtonsoft.Json;
using SanteDB.Client.Disconnected.Data.Synchronization;
using System.Xml.Serialization;

namespace SanteDB.Client.Disconnected.Rest.Model
{
    /// <summary>
    /// Wrapper for REST interactions of <see cref="ISynchronizationQueue"/>
    /// </summary>
    [XmlType(nameof(SynchronizationQueueInfo), Namespace = "http://santedb.org/ami")]
    public class SynchronizationQueueInfo
    {

        public SynchronizationQueueInfo()
        {

        }

        public SynchronizationQueueInfo(ISynchronizationQueue synchronizationQueue)
        {
            this.Name = synchronizationQueue.Name;
            this.Pattern = synchronizationQueue.Type;
        }

        /// <summary>
        /// Gets or sets the name of the queue
        /// </summary>
        [XmlElement("name"), JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the pattern
        /// </summary>
        [XmlElement("pattern"), JsonProperty("pattern")]
        public SynchronizationPattern Pattern { get; set; }
    }
}