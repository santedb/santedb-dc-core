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
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model
{
    /// <summary>
    /// Versioned data
    /// </summary>
    public abstract class DbVersionedData : DbBaseData
    {
        /// <summary>
        /// Gets or sets the server version UUID, this is used to ensure that the version on a server
        /// equals the version here
        /// </summary>
        /// <value>The version UUID.</value>
        [Column("version_uuid"), MaxLength(16), NotNull]
        public byte[] VersionUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the version key.
        /// </summary>
        /// <value>The version key.</value>
        [Ignore]
        public Guid VersionKey
        {
            get { return this.VersionUuid == null ? Guid.Empty : new Guid(this.VersionUuid); }
            set { this.VersionUuid = value.ToByteArray(); }
        }

        /// <summary>
        /// Replace previous version uuid
        /// </summary>
        [Column("replace_version_uuid"), MaxLength(16)]
        public byte[] PreviousVersionUuid { get; set; }


        /// <summary>
        /// Gets or sets the version key.
        /// </summary>
        /// <value>The version key.</value>
        [Ignore]
        public Guid PreviousVersionKey
        {
            get { return this.PreviousVersionUuid == null ? Guid.Empty : new Guid(this.PreviousVersionUuid); }
            set { this.PreviousVersionUuid = value.ToByteArray(); }
        }

        /// <summary>
        /// The last known version sequence
        /// </summary>
        [Column("last_version_sequence")]
        public int VersionSequenceId { get; set; }

    }
}

