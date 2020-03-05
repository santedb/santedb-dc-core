﻿/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
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
using SanteDB.Core.Data.QueryBuilder.Attributes;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Security.Audit.Model
{
    /// <summary>
    /// Represents a target object
    /// </summary>
    [Table("audit_object")]
    public class DbAuditObject
    {
        /// <summary>
        /// Identifier of the object
        /// </summary>
        [Column("id"), PrimaryKey]
        public byte[] Id { get; set; }

        /// <summary>
        /// Gets or sets the audit identifier
        /// </summary>
        [Column("audit_id"), ForeignKey(typeof(DbAuditData), nameof(DbAuditData.Id))]
        public byte[] AuditId { get; set; }

        /// <summary>
        /// The identifier of the object
        /// </summary>
        [Column("obj_id")]
        public string ObjectId { get; set; }

        /// <summary>
        /// The object type identifier
        /// </summary>
        [Column("obj_typ")]
        public int Type { get; set; }

        /// <summary>
        /// Gets or sets the role
        /// </summary>
        [Column("rol")]
        public int Role { get; set; }

        /// <summary>
        /// The lifecycle
        /// </summary>
        [Column("lifecycle")]
        public int LifecycleType { get; set; }

        /// <summary>
        /// Identifier type code
        /// </summary>
        [Column("id_type")]
        public int IDTypeCode { get; set; }

        /// <summary>
        /// The query associated
        /// </summary>
        [Column("query")]
        public String QueryData { get; set; }

        /// <summary>
        /// The name data associated 
        /// </summary>
        [Column("name")]
        public String NameData { get; set; }
    }
}
