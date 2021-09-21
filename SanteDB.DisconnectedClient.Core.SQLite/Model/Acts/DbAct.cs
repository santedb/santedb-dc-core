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
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Model.Extensibility;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Acts
{
    /// <summary>
    /// Represents a table which can store act data
    /// </summary>
    [Table("act")]
    [AssociativeTable(typeof(DbSecurityPolicy), typeof(DbActSecurityPolicy))]
    public class DbAct : DbVersionedData, IDbHideable
    {
        /// <summary>
        /// Gets or sets the template
        /// </summary>
        [Column("template"), MaxLength(16), ForeignKey(typeof(DbTemplateDefinition), nameof(DbTemplateDefinition.Uuid))]
        public byte[] TemplateUuid { get; set; }

        /// <summary>
        /// True if negated
        /// </summary>
        [Column("isNegated")]
        public bool IsNegated { get; set; }

        /// <summary>
        /// Identifies the time that the act occurred
        /// </summary>
        [Column("actTime")]
        public DateTimeOffset? ActTime { get; set; }

        /// <summary>
        /// Identifies the start time of the act
        /// </summary>
        [Column("startTime")]
        public DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Identifies the stop time of the act
        /// </summary>
        [Column("stopTime")]
        public DateTimeOffset? StopTime { get; set; }

        /// <summary>
        /// Identifies the class concept
        /// </summary>
        [Column("classConcept"), MaxLength(16), NotNull, ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] ClassConceptUuid { get; set; }

        /// <summary>
        /// Gets or sets the mood of the act
        /// </summary>
        [Column("moodConcept"), MaxLength(16), NotNull, ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] MoodConceptUuid { get; set; }

        /// <summary>
        /// Gets or sets the reason concept
        /// </summary>
        [Column("reasonConcept"), MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] ReasonConceptUuid { get; set; }

        /// <summary>
        /// Gets or sets the status concept
        /// </summary>
        [Column("statusConcept"), MaxLength(16), NotNull, ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] StatusConceptUuid { get; set; }

        /// <summary>
        /// Gets or sets the type concept
        /// </summary>
        [Column("typeConcept"), MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] TypeConceptUuid { get; set; }

        /// <summary>
        /// Hidden column
        /// </summary>
        [Column("hidden")]
        public bool Hidden { get; set; }
    }
}
