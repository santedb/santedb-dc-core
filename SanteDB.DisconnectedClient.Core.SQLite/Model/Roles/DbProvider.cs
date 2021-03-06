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
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SQLite.Net.Attributes;

namespace SanteDB.DisconnectedClient.SQLite.Model.Roles
{
    /// <summary>
    /// Represents a health care provider in the database
    /// </summary>
    [Table("provider")]
    public class DbProvider : DbPersonSubTable
    {

        /// <summary>
        /// Gets or sets the specialty.
        /// </summary>
        /// <value>The specialty.</value>
        [Column("specialty"), MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] Specialty
        {
            get;
            set;
        }

        public class QueryResult : DbPerson.QueryResult
        {

            /// <summary>
            /// Gets or sets the specialty.
            /// </summary>
            /// <value>The specialty.</value>
            [Column("specialty"), MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
            public byte[] Specialty
            {
                get;
                set;
            }

        }
    }
}

