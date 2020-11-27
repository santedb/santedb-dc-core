/*
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Security
{
    /// <summary>
    /// Represents a security device. This table should only have one row (the current device)
    /// </summary>
    [Table("security_device")]
    [AssociativeTable(typeof(DbSecurityPolicy), typeof(DbSecurityDevicePolicy))]
    public class DbSecurityDevice : DbBaseData
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

        /// <summary>
        /// Gets the secret 
        /// </summary>
        [Column("secret")]
        public String DeviceSecret { get; set; }

        /// <summary>
        /// Gets or sets the invalid authentication attempts.
        /// </summary>
        /// <value>The invalid authentication attempts.</value>
        [Column("invalid_auth")]
        public int InvalidAuthAttempts
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this object is ocked
        /// </summary>
        /// <value><c>true</c> if lockout enabled; otherwise, <c>false</c>.</value>
        [Column("locked")]
        public DateTime? Lockout
        {
            get;
            set;
        }

        /// <summary>
        /// Last authentication time
        /// </summary>
        [Column("last_auth")]
        public DateTime? LastAuthTime { get; set; }
    }
}

