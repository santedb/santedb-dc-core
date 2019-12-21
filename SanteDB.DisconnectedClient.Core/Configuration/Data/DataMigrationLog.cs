﻿/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Core.Configuration.Data
{
    /// <summary>
    /// Data migration log
    /// </summary>
    [XmlType(nameof(DataMigrationLog), Namespace = "http://santedb.org/mobile/configuration")]
    [XmlRoot(nameof(DataMigrationLog), Namespace = "http://santedb.org/mobile/configuration")]
    public class DataMigrationLog
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Core.Configuration.Data.DataMigrationLog"/> class.
        /// </summary>
        public DataMigrationLog()
        {
            this.Entry = new List<DataMigrationEntry>();
        }

        /// <summary>
        /// Gets or sets the entry.
        /// </summary>
        [XmlElement("entry"), JsonProperty("entry")]
        public List<DataMigrationEntry> Entry
        {
            get;
            set;
        }

        /// <summary>
        /// Data migration entry
        /// </summary>
        [XmlType(nameof(DataMigrationLog), Namespace = "http://santedb.org/mobile/data")]
        public class DataMigrationEntry
        {

            /// <summary>
            /// Initializes a new instance of the
            /// <see cref="SanteDB.DisconnectedClient.Core.Configuration.Data.DataMigrationLog+DataMigrationEntry"/> class.
            /// </summary>
            public DataMigrationEntry()
            {

            }

            /// <summary>
            /// Initializes a new instance of the
            /// <see cref="SanteDB.DisconnectedClient.Core.Configuration.Data.DataMigrationLog+DataMigrationEntry"/> class.
            /// </summary>
            /// <param name="migration">Migration.</param>
            public DataMigrationEntry(IDbMigration migration)
            {
                this.Id = migration.Id;
                this.Date = DateTime.Now;
            }

            /// <summary>
            /// Gets or sets the identifier of the migration
            /// </summary>
            /// <value>The identifier.</value>
            [XmlAttribute("id"), JsonProperty("id")]
            public String Id
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the date when the entry was installed
            /// </summary>
            [XmlAttribute("date"), JsonProperty("date")]
            public DateTime Date
            {
                get;
                set;
            }


        }
    }
}

