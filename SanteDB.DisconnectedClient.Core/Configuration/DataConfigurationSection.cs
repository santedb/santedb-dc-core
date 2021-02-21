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
using System.Collections.Generic;
using System.Xml.Serialization;
using Newtonsoft.Json;
using SanteDB.Core.Configuration.Data;

namespace SanteDB.DisconnectedClient.Configuration.Data
{
    /// <summary>
    /// Data configuration section
    /// </summary>
    [XmlType(nameof(DcDataConfigurationSection), Namespace = "http://santedb.org/mobile/configuration")]
    public class DcDataConfigurationSection : DataConfigurationSection
    {
	    /// <summary>
        /// Initializes a new instance of the data configuration section
        /// </summary>
        public DcDataConfigurationSection()
        {
            this.MigrationLog = new DataMigrationLog();
        }

	    /// <summary>
        /// Gets or sets the name of the alerts data store
        /// </summary>
        [XmlAttribute("mailDataStore")][JsonIgnore]
        public string MailDataStore
        {
            get;
            set;
        }


	    /// <summary>
        /// Gets or sets the name of the main data source connection string.
        /// </summary>
        /// <value>The name of the main data source connection string.</value>
        [XmlAttribute("clinicalDataStore")][JsonIgnore]
        public string MainDataSourceConnectionStringName
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the name of the message queue connection string.
        /// </summary>
        /// <value>The name of the message queue connection string.</value>
        [XmlAttribute("messageQueue")][JsonIgnore]
        public string MessageQueueConnectionStringName
        {
            get;
            set;
        }


	    /// <summary>
        /// Migration log 
        /// </summary>
        /// <value>The migration log.</value>
        [XmlElement("migration")][JsonProperty("migration")]
        public DataMigrationLog MigrationLog
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the options
        /// </summary>
        [XmlIgnore][JsonProperty("options")]
        public Dictionary<string, object> Options { get; set; }

	    /// <summary>
        /// Gets the configuration view model provider
        /// </summary>
        [XmlIgnore][JsonProperty("provider")]
        public string Provider { get; set; }
    }
    

}

