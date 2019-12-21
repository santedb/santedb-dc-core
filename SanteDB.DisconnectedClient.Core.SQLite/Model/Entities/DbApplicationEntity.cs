/*
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
using SanteDB.Core.Data.QueryBuilder.Attributes;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Entities
{
    /// <summary>
    /// Represents an entity which is used to represent an application
    /// </summary>
    [Table("application")]
    public class DbApplicationEntity : DbEntitySubTable
    {
        /// <summary>
        /// Gets or sets the security application.
        /// </summary>
        /// <value>The security application.</value>
        [Column("securityApplication"), MaxLength(16), ForeignKey(typeof(DbSecurityApplication), nameof(DbSecurityApplication.Uuid))]
        public byte[] SecurityApplicationUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the software.
        /// </summary>
        /// <value>The name of the software.</value>
        [Column("name"), NotNull]
        public String SoftwareName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the version.
        /// </summary>
        /// <value>The name of the version.</value>
        [Column("version")]
        public String VersionName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the vendor.
        /// </summary>
        /// <value>The name of the vendor.</value>
        [Column("vendor")]
        public String VendorName
        {
            get;
            set;
        }
    }
}

