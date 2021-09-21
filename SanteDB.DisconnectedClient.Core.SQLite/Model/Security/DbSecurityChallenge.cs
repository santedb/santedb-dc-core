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
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Security
{
    /// <summary>
    /// Defines a security challenge question for the user
    /// </summary>
    [Table("security_challenge")]
    public class DbSecurityChallenge : DbBaseData
    {

        /// <summary>
        /// Gets or sets the challenge text
        /// </summary>
        [Column("text")]
        public String ChallengeText { get; set; }

    }


    /// <summary>
    /// Security user challenge association
    /// </summary>
    [Table("security_user_challenge")]
    public class DbSecurityUserChallengeAssoc
    {

        /// <summary>
        /// Gets the user that is associated with this challenge
        /// </summary>
        [Column("user_uuid")]
        public byte[] UserUuid { get; set; }

        /// <summary>
        /// Gets or sets the associated challenge text
        /// </summary>
        [Column("challenge_uuid")]
        public byte[] ChallengeUuid { get; set; }

        /// <summary>
        /// Challenge response
        /// </summary>
        [Column("response")]
        public String ChallengeResponse { get; set; }

        /// <summary>
        /// The time that the challenge will expire
        /// </summary>
        [Column("expiration")]
        public DateTime? ExpiryTime { get; set; }

        /// <summary>
        /// Query result for authentication
        /// </summary>
        public class QueryResult : DbSecurityUser
        {

            /// <summary>
            /// Challenge response
            /// </summary>
            [Column("response")]
            public String ChallengeResponse { get; set; }
        }

    }

}
