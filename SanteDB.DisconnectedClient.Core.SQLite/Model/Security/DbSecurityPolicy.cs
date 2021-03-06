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

namespace SanteDB.DisconnectedClient.SQLite.Model.Security
{
    /// <summary>
    /// Represents a single security policy
    /// </summary>
    [Table("security_policy")]
    public class DbSecurityPolicy : DbIdentified
    {

        /// <summary>
        /// Gets or sets the handler.
        /// </summary>
        /// <value>The handler.</value>
        [Column("handler")]
        public String Handler
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [Column("name")]
        public String Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is public.
        /// </summary>
        /// <value><c>true</c> if this instance is public; otherwise, <c>false</c>.</value>
        [Column("is_public")]
        public bool IsPublic
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance can override.
        /// </summary>
        /// <value><c>true</c> if this instance can override; otherwise, <c>false</c>.</value>
        [Column("can_override")]
        public bool CanOverride
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the policy oid
        /// </summary>
        [Column("oid"), Unique]
        public String Oid
        {
            get;
            set;
        }

        /// <summary>
        /// Represents the result of a query 
        /// </summary>
        public class DbSecurityPolicyInstanceQueryResult : DbSecurityPolicy
        {

            /// <summary>
            /// The grant
            /// </summary>
            [Column("grant_type")]
            public int GrantType { get; set; }

        }

    }
}

