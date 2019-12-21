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
 * Date: 2019-12-18
 */
using SanteDB.Core.Data.QueryBuilder.Attributes;
using SQLite.Net.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Security.Audit.Model
{
    /// <summary>
    /// Audit metadata
    /// </summary>
    [Table("audit_metadata")]
    public class DbAuditMetadata
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
        /// Metadata key for audits
        /// </summary>
        [Column("attr")]
        public int MetadataKey { get; set; }

        /// <summary>
        /// The value of the audit metadata
        /// </summary>
        [Column("val")]
        public string Value { get; set; }
    }
}
