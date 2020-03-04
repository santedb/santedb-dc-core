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
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Roles
{
    /// <summary>
    /// Represents a patient in the SQLite store
    /// </summary>
    [Table("patient")]
    public class DbPatient : DbPersonSubTable
    {

        /// <summary>
        /// Gets or sets the gender concept
        /// </summary>
        /// <value>The gender concept.</value>
        [Column("genderConcept"), NotNull, MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] GenderConceptUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the deceased date.
        /// </summary>
        /// <value>The deceased date.</value>
        [Column("deceasedDate")]
        public DateTime? DeceasedDate
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the deceased date precision.
        /// </summary>
        /// <value>The deceased date precision.</value>
        [Column("deceasedDatePrevision"), MaxLength(1)]
        public string DeceasedDatePrecision
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the multiple birth order.
        /// </summary>
        /// <value>The multiple birth order.</value>
        [Column("birth_order")]
        public int? MultipleBirthOrder
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the marital status code
        /// </summary>
        [Column("marital_status")]
        public byte[] MaritalStatusUuid { get; set; }

        /// <summary>
        /// Gets or sets the education level key
        /// </summary>
        [Column("education_level")]
        public byte[] EducationLevelUuid { get; set; }

        /// <summary>
        /// Gets or sets the living arrangement key
        /// </summary>
        [Column("living_arrangement")]
        public byte[] LivingArrangementUuid { get; set; }

        /// <summary>
        /// Gets or sets the ethnic group code id
        /// </summary>
        [Column("ethnicity")]
        public byte[] EthnicGroupCodeUuid { get; set; }

        /// <summary>
        /// Query result for patient
        /// </summary>
        public class QueryResult : DbPerson.QueryResult
        {

            /// <summary>
            /// Gets or sets the gender concept
            /// </summary>
            /// <value>The gender concept.</value>
            [Column("genderConcept"), NotNull, MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
            public byte[] GenderConceptUuid
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the deceased date.
            /// </summary>
            /// <value>The deceased date.</value>
            [Column("deceasedDate")]
            public DateTime? DeceasedDate
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the deceased date precision.
            /// </summary>
            /// <value>The deceased date precision.</value>
            [Column("deceasedDatePrevision"), MaxLength(1)]
            public string DeceasedDatePrecision
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the multiple birth order.
            /// </summary>
            /// <value>The multiple birth order.</value>
            [Column("birth_order")]
            public int? MultipleBirthOrder
            {
                get;
                set;
            }
        }
    }
}

