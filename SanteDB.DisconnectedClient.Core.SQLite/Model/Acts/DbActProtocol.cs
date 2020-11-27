/*
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
using SQLite.Net.Attributes;

namespace SanteDB.DisconnectedClient.SQLite.Model.Acts
{
    /// <summary>
    /// Represents an act protocol
    /// </summary>
    [Table("act_protocol")]
    public class DbActProtocol : DbIdentified
    {

        /// <summary>
        /// Gets or sets the act UUID
        /// </summary>
        [Column("act_uuid"), MaxLength(16), Indexed, ForeignKey(typeof(DbAct), nameof(DbAct.Uuid))]
        public byte[] SourceUuid { get; set; }

        /// <summary>
        /// Gets or sets the protocol uuid
        /// </summary>
        [Column("proto_uuid"), MaxLength(16)]
        public byte[] ProtocolUuid { get; set; }

        /// <summary>
        /// Represents the sequence of the item
        /// </summary>
        [Column("sequence")]
        public int Sequence { get; set; }
    }
}
