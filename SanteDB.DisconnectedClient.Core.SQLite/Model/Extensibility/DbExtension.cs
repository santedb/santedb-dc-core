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
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Extensibility
{
    /// <summary>
    /// Extension.
    /// </summary>
    public abstract class DbExtension : DbIdentified
    {

        /// <summary>
        /// Gets or sets the extension identifier.
        /// </summary>
        /// <value>The extension identifier.</value>
        [Column("extensionType"), NotNull, MaxLength(16), ForeignKey(typeof(DbExtensionType), nameof(DbExtensionType.Uuid))]
        public byte[] ExtensionTypeUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [Column("value")]
        public byte[] Value
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the display value of the object
        /// </summary>
        [Column("display")]
        public String ExtensionDisplay { get; set; }

    }

    /// <summary>
    /// Entity extension.
    /// </summary>
    [Table("entity_extension")]
    public class DbEntityExtension : DbExtension
    {

        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        /// <value>The source identifier.</value>
        [Column("entity_uuid"), NotNull, Indexed, MaxLength(16), ForeignKey(typeof(DbEntity), nameof(DbEntity.Uuid))]
        public byte[] SourceUuid
        {
            get;
            set;
        }

    }

    /// <summary>
    /// Act extensions
    /// </summary>
    [Table("act_extension")]
    public class DbActExtension : DbExtension
    {
        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        /// <value>The source identifier.</value>
        [Column("act_uuid"), NotNull, Indexed, MaxLength(16), ForeignKey(typeof(DbAct), nameof(DbAct.Uuid))]
        public byte[] SourceUuid
        {
            get;
            set;
        }
    }
}

