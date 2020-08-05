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
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.DataType
{
    /// <summary>
    /// Represents an assigning authority
    /// </summary>
    [Table("assigning_authority")]
    [AssociativeTable(typeof(DbConcept), typeof(DbAuthorityScope))]
    public class DbAssigningAuthority : DbBaseData
    {

        /// <summary>
        /// Gets or sets the name of the aa
        /// </summary>
        [Column("name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the short HL7 code of the AA
        /// </summary>
        [Column("domainName"), Indexed, MaxLength(32)]
        public String DomainName { get; set; }

        /// <summary>
        /// Gets or sets the OID of the AA
        /// </summary>
        [Column("oid")]
        public String Oid { get; set; }

        /// <summary>
        /// Gets or sets the description of the AA
        /// </summary>
        [Column("description")]
        public String Description { get; set; }

        /// <summary>
        /// Gets or sets the URL of AA
        /// </summary>
        [Column("url")]
        public String Url { get; set; }

        /// <summary>
        /// Assigning device identifier
        /// </summary>
        [Column("assigningApplicationId"), ForeignKey(typeof(DbSecurityApplication), nameof(DbSecurityApplication.Uuid))]
        public byte[] AssigningApplicationUuid { get; set; }

        /// <summary>
        /// Gets or sets the policy identifier for the assigning authority
        /// </summary>
        [Column("policyId"), ForeignKey(typeof(DbSecurityPolicy), nameof(DbSecurityPolicy.Uuid))]
        public byte[] PolicyUuid { get; set; }

        /// <summary>
        /// Validation regex
        /// </summary>
        [Column("val_rgx")]
        public String ValidationRegex { get; set; }

        /// <summary>
        /// True if AA is unique
        /// </summary>
        [Column("is_unique")]
        public bool IsUnique { get; set; }

    }


    /// <summary>
    /// Identifier scope
    /// </summary>
    [Table("assigning_authority_scope")]
    public class DbAuthorityScope : DbIdentified
    {
        /// <summary>
        /// Gets or sets the scope of the auhority
        /// </summary>
        [Column("authority"), MaxLength(16), NotNull, ForeignKey(typeof(DbAssigningAuthority), nameof(DbAssigningAuthority.Uuid))]
        public byte[] AssigningAuthorityUuid { get; set; }

        /// <summary>
        /// Gets or sets the scope of the auhority
        /// </summary>
        [Column("concept"), MaxLength(16), NotNull, ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] ScopeConceptUuid { get; set; }

    }
}
