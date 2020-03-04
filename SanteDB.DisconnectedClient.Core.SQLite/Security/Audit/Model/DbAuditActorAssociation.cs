/*
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

namespace SanteDB.DisconnectedClient.SQLite.Security.Audit.Model
{
    /// <summary>
    /// Associates the audit actor to audit message
    /// </summary>
    [Table("audit_actor_assoc")]
    public class DbAuditActorAssociation
    {
        /// <summary>
        /// Id of the association
        /// </summary>
        [Column("id"), PrimaryKey]
        public byte[] Id { get; set; }

        /// <summary>
        /// Audit identifier
        /// </summary>
        [Column("audit_id"), NotNull, Indexed, ForeignKey(typeof(DbAuditData), nameof(DbAuditData.Id))]
        public byte[] SourceUuid { get; set; }

        /// <summary>
        /// Actor identifier
        /// </summary>
        [Column("actor_id"), NotNull, ForeignKey(typeof(DbAuditActor), nameof(DbAuditActor.Id))]
        public byte[] TargetUuid { get; set; }


        /// <summary>
        /// True if user is requestor
        /// </summary>
        [Column("is_requestor")]
        public bool UserIsRequestor { get; set; }

        /// <summary>
        /// Gets or sets the access point
        /// </summary>
        [Column("ap")]
        public string AccessPoint { get; set; }
    }
}
