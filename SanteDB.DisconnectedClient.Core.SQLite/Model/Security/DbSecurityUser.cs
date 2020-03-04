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
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model.Security
{
    /// <summary>
    /// Represents a user for the purpose of authentication
    /// </summary>
    [Table("security_user")]
    [AssociativeTable(typeof(DbSecurityRole), typeof(DbSecurityUserRole))]

    public class DbSecurityUser : DbBaseData
    {

        /// <summary>
        /// Gets or sets the email.
        /// </summary>
        /// <value>The email.</value>
        [Column("email"), Collation("NOCASE")]
        public String Email
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the invalid login attempts.
        /// </summary>
        /// <value>The invalid login attempts.</value>
        [Column("invalid_logins")]
        public int InvalidLoginAttempts
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="SanteDB.DisconnectedClient.SQLite.Model.Security.DbSecurityUser"/> lockout enabled.
        /// </summary>
        /// <value><c>true</c> if lockout enabled; otherwise, <c>false</c>.</value>
        [Column("locked")]
        public DateTime? Lockout
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the password hash.
        /// </summary>
        /// <value>The password hash.</value>
        [Column("password"), NotNull, Indexed]
        public String Password
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a PIN hash which can only be used on this device
        /// </summary>
        [Column("pin"), Indexed]
        public string PinHash { get; set; }

        /// <summary>
        /// Gets or sets the security stamp.
        /// </summary>
        /// <value>The security stamp.</value>
        [Column("security_stamp")]
        public String SecurityHash
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is two factor enabled.
        /// </summary>
        /// <value><c>true</c> if this instance is two factor enabled; otherwise, <c>false</c>.</value>
        [Column("tfa_enabled")]
        public bool TwoFactorEnabled
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the user.
        /// </summary>
        /// <value>The name of the user.</value>
        [Column("username"), NotNull]
        public String UserName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the last login.
        /// </summary>
        /// <value>The last login.</value>
        [Column("last_login")]
        public DateTime? LastLoginTime
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the phone number.
        /// </summary>
        /// <value>The phone number.</value>
        [Column("phone_number"), Collation("NOCASE")]
        public String PhoneNumber
        {
            get;
            set;
        }

    }

    /// <summary>
    /// Associative entity between security user and role
    /// </summary>
    [Table("security_user_role")]
    public class DbSecurityUserRole : DbIdentified
    {
        /// <summary>
        /// Gets or sets the role identifier
        /// </summary>
        /// <value>The role identifier.</value>
        [Column("role_id"), Indexed(Name = "security_user_role_user_role", Unique = true), MaxLength(16), ForeignKey(typeof(DbSecurityRole), nameof(DbSecurityRole.Uuid))]
        public byte[] RoleUuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the user identifier
        /// </summary>
        /// <value>The user identifier.</value>
        [Column("user_id"), Indexed(Name = "security_user_role_user_role", Unique = true), MaxLength(16), ForeignKey(typeof(DbSecurityUser), nameof(DbSecurityUser.Uuid))]
        public byte[] UserUuid
        {
            get;
            set;
        }


    }
}

