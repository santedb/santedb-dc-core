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
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Security
{
    /// <summary>
    /// Security application data. Should only be one entry here as well
    /// </summary>
    [Table("security_application")]
    [AssociativeTable(typeof(DbSecurityPolicy), typeof(DbSecurityApplicationPolicy))]
    public class DbSecurityApplication : DbBaseData
    {

        /// <summary>
        /// Gets or sets the public identifier.
        /// </summary>
        /// <value>The public identifier.</value>
        [Column("public_id"), Unique]
        public String PublicId
        {
            get;
            set;
        }
    }
}

