/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using SanteDB.Core.Configuration.Data;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SQLite.Net;
using SQLite.Net.Interop;
using System;
using System.Collections.Generic;

namespace SanteDB.DisconnectedClient.SQLite.Connection
{
    /// <summary>
    /// Lockable sqlite connection
    /// </summary>
    public abstract class LockableSQLiteConnection : SQLiteConnection
    {

        // Lock count
        protected int m_lockCount = 0;

        /// <summary>
        /// When true the connection stays open
        /// </summary>
        public bool Persistent { get; set; }

        /// <summary>
        /// Get the connection string
        /// </summary>
        public ConnectionString ConnectionString { get; }

        /// <summary>
        /// Constructor for locable sqlite connection
        /// </summary>
        public LockableSQLiteConnection(ISQLitePlatform sqlitePlatform, ConnectionString connectionString, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks = true, IBlobSerializer serializer = null, IDictionary<String, TableMapping> tableMappings = null, IDictionary<Type, String> extraTypeMappings = null, IContractResolver resolver = null) :
            base(sqlitePlatform, connectionString.GetComponent("dbfile"), openFlags, storeDateTimeAsTicks, serializer, tableMappings, extraTypeMappings, resolver, connectionString.GetComponent("encrypt")?.ToLower() == "true" ? ApplicationContext.Current.GetCurrentContextSecurityKey() : null)
        {
            this.ConnectionString = connectionString;
        }

        /// <summary>
        /// Locks the connection file
        /// </summary>
        public abstract IDisposable Lock();
    }
}
