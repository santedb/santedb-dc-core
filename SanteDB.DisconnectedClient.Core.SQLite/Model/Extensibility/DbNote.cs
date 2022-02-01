/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Extensibility
{
    /// <summary>
    /// Represents note storage
    /// </summary>
    public abstract class DbNote : DbIdentified
    {

        /// <summary>
		/// Gets or sets the author identifier.
		/// </summary>
		/// <value>The author identifier.</value>
		[Column("author"), MaxLength(16), NotNull, ForeignKey(typeof(DbPerson), nameof(DbPerson.Uuid))]
        public byte[] AuthorUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        /// <value>The text.</value>
        [Column("text")]
        public String Text
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Entity note.
    /// </summary>
    [Table("entity_note")]
    public class DbEntityNote : DbNote
    {
        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        /// <value>The source identifier.</value>
        [Column("entity_uuid"), Indexed, NotNull, MaxLength(16), ForeignKey(typeof(DbEntity), nameof(DbEntity.Uuid))]
        public byte[] EntityUuid
        {
            get;
            set;
        }

    }

    /// <summary>
    /// Act note.
    /// </summary>
    [Table("act_note")]
    public class DbActNote : DbNote
    {
        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        /// <value>The source identifier.</value>
        [Column("act_uuid"), Indexed, NotNull, MaxLength(16), ForeignKey(typeof(DbAct), nameof(DbAct.Uuid))]
        public byte[] SourceUuid
        {
            get;
            set;
        }

    }
}

