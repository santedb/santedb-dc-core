/*
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
using SanteDB.Core.Configuration.Data;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SQLite.Net;
using SQLite.Net.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using SanteDB.DisconnectedClient.SQLite.Query;
using TableMapping = SQLite.Net.TableMapping;

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

            try
            {
                // Try to init extended filters
                foreach (var f in AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.IsDynamic)
                        .SelectMany(a => { try { return a.ExportedTypes; } catch { return Type.EmptyTypes; } })
                        .Where(t => typeof(IDbFilterFunction).IsAssignableFrom(t) && !t.IsAbstract)
                        .Select(t => Activator.CreateInstance(t) as IDbFilterFunction))
                    f.Initialize(this);
                        
            }
            catch
            {

            }
        }

        /// <summary>
        /// Locks the connection file
        /// </summary>
        public abstract IDisposable Lock();
    }
}
