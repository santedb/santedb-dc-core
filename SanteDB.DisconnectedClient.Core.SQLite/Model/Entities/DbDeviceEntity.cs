﻿/*
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
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Entities
{
    /// <summary>
    /// Represents the entity representation of an object
    /// </summary>
    [Table("device")]
    public class DbDeviceEntity : DbEntitySubTable
    {

        /// <summary>
        /// Gets or sets the security device identifier.
        /// </summary>
        /// <value>The security device identifier.</value>
        [Column("securityDevice"), MaxLength(16), ForeignKey(typeof(DbSecurityDevice), nameof(DbSecurityDevice.Uuid))]
        public byte[] SecurityDeviceUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the manufacturer model.
        /// </summary>
        /// <value>The name of the manufacturer model.</value>
        [Column("manufacturer")]
        public string ManufacturerModelName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the operating system.
        /// </summary>
        /// <value>The name of the operating system.</value>
        [Column("operatingSystem")]
        public String OperatingSystemName
        {
            get;
            set;
        }
    }
}

