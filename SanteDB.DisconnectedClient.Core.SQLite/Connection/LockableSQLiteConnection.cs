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
using SanteDB.Core.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Query;
using SQLite.Net;
using SQLite.Net.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TableMapping = SQLite.Net.TableMapping;

namespace SanteDB.DisconnectedClient.SQLite.Connection
{
    /// <summary>
    /// Lockable sqlite connection
    /// </summary>
    public class LockableSQLiteConnection : SQLiteConnection
    {

        // Lock count
        protected int m_lockCount = 0;

        // Lock object
        private object m_lockObject = new object();

#if DEBUG
        private Int32? m_claimedBy;
#endif

        // Available event
        private ManualResetEventSlim m_availableEvent = new ManualResetEventSlim(true);

        /// <summary>
        /// Get the number of locks
        /// </summary>
        internal int LockCount => m_lockCount;

        /// <summary>
        /// Get the connection string
        /// </summary>
        public ConnectionString ConnectionString { get; }

        /// <summary>
        /// True if the connection is readonly
        /// </summary>
        public bool IsReadonly { get; }

        /// <summary>
        /// True if the connection is entered on this thread
        /// </summary>
        public bool IsEntered => Monitor.IsEntered(this.m_lockObject);

        /// <summary>
        /// True if disposed
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Constructor for locable sqlite connection
        /// </summary>
        public LockableSQLiteConnection(ISQLitePlatform sqlitePlatform, ConnectionString connectionString, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks = true, IBlobSerializer serializer = null, IDictionary<String, TableMapping> tableMappings = null, IDictionary<Type, String> extraTypeMappings = null, IContractResolver resolver = null) :
            base(sqlitePlatform, connectionString.GetComponent("dbfile"), openFlags, storeDateTimeAsTicks, serializer, tableMappings, extraTypeMappings, resolver, connectionString.GetComponent("encrypt")?.ToLower() == "true" ? ApplicationContext.Current.GetCurrentContextSecurityKey() : null)
        {
            this.BusyTimeout = new TimeSpan(0, 0, 10);
            this.ConnectionString = connectionString;
            this.IsReadonly = openFlags.HasFlag(SQLiteOpenFlags.ReadOnly);

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
        /// Represents a sqlite lock box
        /// </summary>
        private class SQLiteLockBox : IDisposable
        {

            // The connection
            private LockableSQLiteConnection m_connection;

            /// <summary>
            /// Call to lock box increases lock
            /// </summary>
            /// <param name="wrappedConnection"></param>
            public SQLiteLockBox(LockableSQLiteConnection wrappedConnection)
            {
                this.m_connection = wrappedConnection;
                Monitor.Enter(this.m_connection.m_lockObject);
#if DEBUG
                this.m_connection.m_claimedBy = Thread.CurrentThread.ManagedThreadId;
#endif
                this.m_connection.m_lockCount++;
                this.m_connection.m_availableEvent.Reset();
            }

            /// <summary>
            /// Dispose of the lock
            /// </summary>
            public void Dispose()
            {
                Monitor.Exit(this.m_connection.m_lockObject);
                this.m_connection.m_lockCount--;
                if (this.m_connection.m_lockCount == 0)
                {
#if DEBUG
                    this.m_connection.m_claimedBy = null;
#endif
                    this.m_connection.m_availableEvent.Set();
                }

            }
        }

        /// <summary>
        /// Locks the connection file
        /// </summary>
        public IDisposable Lock()
        {
            return new SQLiteLockBox(this);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            lock (this.m_lockObject)
            {
#if DEBUG
                this.m_claimedBy = null;
#endif
                base.Dispose(disposing);
                this.IsDisposed = true;
            }
        }

        /// <summary>
        /// Wait for the connection to become available
        /// </summary>
        public bool Wait()
        {
            if (!this.IsEntered)
                this.m_availableEvent.Wait();
            return true;
        }

        /// <summary>
        /// Represent this as a string
        /// </summary>
        public override string ToString()
        {
            return $"DB = {this.ConnectionString.Name} ; IsDisposed = {this.IsDisposed} ; IsEntered = {this.IsEntered} ; Lock = {this.LockCount} ; - {Thread.CurrentThread.ManagedThreadId} ";
        }
    }
}
