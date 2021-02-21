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
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Extensibility
{
    /// <summary>
    /// Represents a simpe tag (version independent)
    /// </summary>
    public abstract class DbTag : DbIdentified
    {


        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        /// <value>The key.</value>
        [Column("key"), NotNull]
        public String TagKey
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [Column("value"), NotNull]
        public String Value
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Represents a tag associated with an enttiy
    /// </summary>
    [Table("entity_tag")]
    public class DbEntityTag : DbTag
    {
        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        /// <value>The source.</value>
        [Column("entity_uuid"), NotNull, Indexed, MaxLength(16), ForeignKey(typeof(DbEntity), nameof(DbEntity.Uuid))]
        public byte[] SourceUuid
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Represents a tag associated with an act
    /// </summary>
    [Table("act_tag")]
    public class DbActTag : DbTag
    {
        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        /// <value>The source.</value>
        [Column("act_uuid"), NotNull, Indexed, MaxLength(16), ForeignKey(typeof(DbAct), nameof(DbAct.Uuid))]
        public byte[] SourceUuid
        {
            get;
            set;
        }
    }

}

