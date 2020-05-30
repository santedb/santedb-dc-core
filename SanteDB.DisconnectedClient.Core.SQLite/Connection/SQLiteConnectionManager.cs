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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using SQLite.Net;
using SQLite.Net.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using SanteDB.DisconnectedClient.Configuration.Data;
using System.Reflection;
using SQLite.Net.Attributes;
using SanteDB.DisconnectedClient.SQLite.Model;

namespace SanteDB.DisconnectedClient.SQLite.Connection
{

    /// <summary>
    /// SQLiteConnectionManager
    /// </summary>
    public class SQLiteConnectionManager : IDataConnectionManager
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "SQLite Data Connection Pool Service";

        // Connection pool
        private List<LockableSQLiteConnection> m_connectionPool = new List<LockableSQLiteConnection>();

        // Write connections
        private Dictionary<String, WriteableSQLiteConnection> m_writeConnections = new Dictionary<string, WriteableSQLiteConnection>();

        /// <summary>
        /// Un-register a readonly connection
        /// </summary>
        internal void UnregisterReadonlyConnection(ReadonlySQLiteConnection conn)
        {
            this.UnregisterConnection(conn);
        }

        /// <summary>
        /// Unregister connection
        /// </summary>
        private void UnregisterConnection(LockableSQLiteConnection conn)
        {
            List<LockableSQLiteConnection> connections = this.GetOrRegisterConnections(conn.ConnectionString);
            lock (s_lockObject)
            {
                Monitor.Exit(conn);
                connections.Remove(conn);

                // Add connection back onto the pool
                if (conn.Persistent)
                    this.m_connectionPool.Add(conn);

                this.m_tracer.TraceVerbose("-- {0} ({1})", conn.DatabasePath, connections.Count);

            }
        }

        /// <summary>
        /// Un-register a readonly connection
        /// </summary>
        internal void RegisterReadonlyConnection(ReadonlySQLiteConnection conn)
        {
            List<LockableSQLiteConnection> connections = this.GetOrRegisterConnections(conn.ConnectionString);

            // Are there other connections that this thread owns?
            bool skipTrafficStop = false;
            WriteableSQLiteConnection writerConnection = null;
            lock (s_lockObject)
                skipTrafficStop = this.m_writeConnections.TryGetValue(conn.DatabasePath, out writerConnection) && Monitor.IsEntered(writerConnection) ||
                    connections.Any(o => Monitor.IsEntered(o));
            if (!skipTrafficStop) // then we must adhere to traffic jams
            {
                var mre = this.GetOrRegisterResetEvent(conn);
                mre.Wait();
                this.RegisterConnection(conn);
            }
        }

        /// <summary>
        /// Register connection
        /// </summary>
        private void RegisterConnection(LockableSQLiteConnection conn)
        {
            List<LockableSQLiteConnection> connections = this.GetOrRegisterConnections(conn.ConnectionString);
            lock (s_lockObject)
            {
                connections.Add(conn);
                this.m_connectionPool.Remove(conn); // Just in-case
                // Lock this connection so I know if I can bypass later
                Monitor.Enter(conn);

                this.m_tracer.TraceVerbose("++ {0} ({1})", conn.DatabasePath, connections.Count);
            }
        }

        /// <summary>
        /// Gets or registers a connection pool
        /// </summary>
        private List<LockableSQLiteConnection> GetOrRegisterConnections(ConnectionString connectionString)
        {
            List<LockableSQLiteConnection> retVal = null;
            if (!this.m_readonlyConnections.TryGetValue(connectionString.Name, out retVal))
            {
                retVal = new List<LockableSQLiteConnection>();
                lock (s_lockObject)
                    if (!this.m_readonlyConnections.ContainsKey(connectionString.Name))
                        this.m_readonlyConnections.Add(connectionString.Name, retVal);
                    else
                        retVal = this.m_readonlyConnections[connectionString.Name];
            }
            return retVal;
        }

        /// <summary>
        /// Un-register a readonly connection
        /// </summary>
        internal void UnregisterWriteConnection(WriteableSQLiteConnection conn)
        {
            var mre = this.GetOrRegisterResetEvent(conn);
            mre.Set();

        }

        /// <summary>
        /// Un-register a readonly connection
        /// </summary>
        internal void RegisterWriteConnection(WriteableSQLiteConnection conn)
        {
            var mre = this.GetOrRegisterResetEvent(conn);
            var connections = this.GetOrRegisterConnections(conn.ConnectionString);
            mre.Reset();
            // Wait for readonly connections to go to 0
            while (connections.Count > 0)
                Task.Delay(100).Wait();

        }

        /// <summary>
        /// Gets or sets the reset event for the particular database
        /// </summary>
        private ManualResetEventSlim GetOrRegisterResetEvent(LockableSQLiteConnection connection)
        {
            ManualResetEventSlim retVal = null;
            if (!this.m_connections.TryGetValue(connection.ConnectionString.Name, out retVal))
            {
                retVal = new ManualResetEventSlim(true);
                lock (s_lockObject)
                    if (!this.m_connections.ContainsKey(connection.ConnectionString.Name))
                        this.m_connections.Add(connection.ConnectionString.Name, retVal);
                    else
                        retVal = this.m_connections[connection.ConnectionString.Name];
            }
            return retVal;
        }

        // connections
        private Dictionary<String, ManualResetEventSlim> m_connections = new Dictionary<string, ManualResetEventSlim>();

        // Readonly connections
        private Dictionary<String, List<LockableSQLiteConnection>> m_readonlyConnections = new Dictionary<string, List<LockableSQLiteConnection>>();

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteConnectionManager));
        // lock
        private static object s_lockObject = new object();
        // instance singleton
        private static SQLiteConnectionManager s_instance = null;

        public event EventHandler Starting;
        public event EventHandler Started;
        public event EventHandler Stopping;
        public event EventHandler Stopped;

        /// <summary>
        /// Gets the current connection manager
        /// </summary>
        public static SQLiteConnectionManager Current
        {
            get
            {
                //if (s_instance == null)
                //    lock (s_lockObject)
                //        if (s_instance == null)
                //        {
                //            s_instance = new SQLiteConnectionManager();
                //            s_instance.Start();
                //        }
                return ApplicationContext.Current.GetService<SQLiteConnectionManager>();
            }
        }

        /// <summary>
        /// True if the daemon is running
        /// </summary>
        public bool IsRunning
        {
            get; private set;
        }

        ///// <summary>
        ///// Release the connection
        ///// </summary>
        ///// <param name="databasePath"></param>
        //public void ReleaseConnection(string databasePath)
        //{
        //    Object lockObject = null;
        //    if (!this.m_locks.TryGetValue(databasePath, out lockObject))
        //        return;
        //    else
        //        Monitor.Exit(lockObject);
        //}

        /// <summary>
        /// SQLLiteConnection manager
        /// </summary>
        public SQLiteConnectionManager()
        {
            s_instance = this;
            this.Start();
        }

        /// <summary>
        /// Get a readonly connection
        /// </summary>
        public LockableSQLiteConnection GetReadonlyConnection(ConnectionString dataSource)
        {
            //return this.GetConnection(dataSource);

            if (!this.IsRunning)
                throw new InvalidOperationException("Cannot get connection before daemon is started");

            // Are there any connections that are open by this source and thread?
            try
            {
                var retVal = this.GetOrCreatePooledConnection(dataSource, true);
                this.m_tracer.TraceVerbose("Readonly connection to {0} established, {1} active connections", dataSource, this.m_connections.Count + this.m_readonlyConnections.Count);

#if DEBUG_SQL
                conn.TraceListener = new TracerTraceListener();
#endif
                return retVal;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting connection: {0}", e);
                throw;
            }

        }

        /// <summary>
        /// Get or create a pooled connection
        /// </summary>
        private LockableSQLiteConnection GetOrCreatePooledConnection(ConnectionString dataSource, bool isReadonly)
        {
            // First is there a connection already?
            var connections = this.GetOrRegisterConnections(dataSource);
            WriteableSQLiteConnection writeConnection = null;
            lock (s_lockObject)
            {
                if (this.m_writeConnections.TryGetValue(dataSource.Name, out writeConnection))
                    if (Monitor.IsEntered(writeConnection)) return writeConnection;
                var conn = connections.FirstOrDefault(o => Monitor.IsEntered(o));
                if (conn != null) return conn;
            }


            ISQLitePlatform platform = ApplicationContext.Current.GetService<ISQLitePlatform>();

            lock (s_lockObject)
            {
                LockableSQLiteConnection retVal = null;
                if (isReadonly)
                    retVal = this.m_connectionPool.OfType<ReadonlySQLiteConnection>().FirstOrDefault(o => o.ConnectionString.Name == dataSource.Name);
                else
                {
                    if (!this.m_writeConnections.TryGetValue(dataSource.Name, out writeConnection)) // Writeable connection can only have one in the pool so if it isn't there make sure it isn't in the current 
                    {
                        writeConnection = new WriteableSQLiteConnection(platform, dataSource, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.Create) { Persistent = true };
                        writeConnection.Execute("PRAGMA synchronous = 1");
                        //writeConnection.Execute("PRAGMA automatic_index = true");
                        //writeConnection.Execute("PRAGMA journal_mode = WAL");
                        this.m_writeConnections.Add(dataSource.Name, writeConnection);
                    }

                    retVal = writeConnection;
                }

                // Remove return value
                if (retVal != null)
                    this.m_connectionPool.Remove(retVal);
                else if (isReadonly)
                {
                    retVal = new ReadonlySQLiteConnection(platform, dataSource, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex) { Persistent = true };
                    //retVal.Execute("PRAGMA threads = 2");
                }
                else
                    throw new InvalidOperationException("Should not be here");
                return retVal;
            }
        }

        /// <summary>
        /// Get connection to the datafile
        /// </summary>
        public LockableSQLiteConnection GetConnection(ConnectionString dataSource)
        {
            if (!this.IsRunning)
                throw new InvalidOperationException("Cannot get connection before daemon is started");

            try
            {
                ISQLitePlatform platform = ApplicationContext.Current.GetService<ISQLitePlatform>();
                var retVal = this.GetOrCreatePooledConnection(dataSource, false);
                this.m_tracer.TraceVerbose("Write connection to {0} established, {1} active connections", dataSource, this.m_connections.Count + this.m_readonlyConnections.Count);
#if DEBUG_SQL
                conn.TraceListener = new TracerTraceListener();
#endif
                return retVal;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting connection: {0}", e);
                throw;
            }
        }


        /// <summary>
        /// Start the connection manager
        /// </summary>
        public bool Start()
        {
            if (this.IsRunning) return true;
            this.Starting?.Invoke(this, EventArgs.Empty);
            this.Started?.Invoke(this, EventArgs.Empty);
            this.IsRunning = true;
            this.Started?.Invoke(this, EventArgs.Empty);
            return this.IsRunning;
        }

        /// <summary>
        /// Stop this service
        /// </summary>
        public bool Stop()
        {
            // Already stopped
            if (!this.IsRunning) return true;

            this.Stopping?.Invoke(this, EventArgs.Empty);

            // Wait for all write connections to finish up
            foreach (var mre in this.m_connections)
            {
                this.m_tracer.TraceVerbose("Waiting for {0} to become free...", mre.Key);
                mre.Value.Wait();
                mre.Value.Reset();
            }

            // Close all readonly connections
            foreach (var itm in this.m_readonlyConnections)
            {
                this.m_tracer.TraceInfo("Waiting for readonly connection {0} to finish up...", itm.Key);
                while (itm.Value.Count > 0)
                    Task.Delay(100).Wait();
            }
            foreach (var itm in this.m_connectionPool)
            {
                this.m_tracer.TraceInfo("Shutting down {0}...", itm.DatabasePath);
                itm.Close();
                itm.Dispose();
            }
            foreach (var itm in this.m_writeConnections)
            {
                this.m_tracer.TraceInfo("Shutting down {0}...", itm.Key);
                itm.Value.Close();
                itm.Value.Dispose();
            }

            this.IsRunning = false;
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Perform a backup of the databases to another location 
        /// </summary>
        /// <param name="passkey">The passkey to set on the backed up databases</param>
        /// <returns>The output folder where backed up files can be located</returns>
        public String Backup(String passkey)
        {
            // Let other threads know they can't open a r/o connection for each db
            try
            {

                var backupDir = Path.Combine(Path.GetTempPath(), "db-copy");
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);

                ISQLitePlatform platform = ApplicationContext.Current.GetService<ISQLitePlatform>();
                var connectionStrings = (ApplicationContext.Current.GetService<IConfigurationManager>()).GetSection<DcDataConfigurationSection>().ConnectionString;
                for (var i = 0; i < connectionStrings.Count; i++)
                {
                    this.m_tracer.TraceInfo("Will backup {0} with passkey {1}", connectionStrings[i].GetComponent("dbfile"), !String.IsNullOrEmpty(passkey));
                    if (!File.Exists(connectionStrings[i].GetComponent("dbfile")))
                        continue;
                    var conn = this.GetConnection(connectionStrings[i]);
                    using (conn.Lock())
                    {
                        ApplicationContext.Current.SetProgress(Strings.locale_backup, (float)i / connectionStrings.Count);

                        var backupFile = Path.Combine(backupDir, Path.GetFileName(conn.DatabasePath));
                        if (File.Exists(backupFile))
                            File.Delete(backupFile);
                        // Create empty database 
                        this.m_tracer.TraceVerbose("Creating temporary copy of database at {0}...", backupFile);

                        // Create table
                        using (var bakConn = new SQLiteConnection(platform, backupFile, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex, key: String.IsNullOrEmpty(passkey) ? null : System.Text.Encoding.UTF8.GetBytes(passkey)))
                        {
                            bakConn.Execute("PRAGMA synchronous = 1");

                            foreach (var tbl in conn.Query<SysInfoStruct>("SELECT name FROM sqlite_master WHERE type='table'"))
                                foreach (var map in typeof(SQLiteConnectionManager).Assembly.GetTypes().Where(o => o.GetCustomAttribute<TableAttribute>(false)?.Name == tbl.Name))
                                {
                                    this.m_tracer.TraceInfo("Creating table structure {0}...", tbl.Name);
                                    bakConn.CreateTable(map);
                                }
                            bakConn.Commit();
                        }

                        try
                        {
                            // Copy the tables to backup
                            if (String.IsNullOrEmpty(passkey))
                                conn.Execute($"ATTACH DATABASE '{backupFile}' AS bak_db KEY ''");
                            else
                                conn.Execute($"ATTACH DATABASE '{backupFile}' AS bak_db KEY '{passkey}'");

                            foreach (var tbl in conn.Query<SysInfoStruct>("SELECT name FROM sqlite_master WHERE type='table'"))
                                foreach (var map in typeof(SQLiteConnectionManager).Assembly.GetTypes().Where(o => o.GetCustomAttribute<TableAttribute>(false)?.Name == tbl.Name))
                                {
                                    try
                                    {
                                        this.m_tracer.TraceInfo("Backing up table {0}...", tbl.Name);
                                        conn.Execute($"INSERT OR IGNORE INTO bak_db.{tbl.Name} SELECT * FROM {tbl.Name}");
                                    }
                                    catch(Exception e)
                                    {
                                        this.m_tracer.TraceWarning("Could not backup {0} - {1}", tbl.Name, e);
                                    }
                                }
                        }
                        finally
                        {
                            conn.Execute("DETACH DATABASE bak_db");
                        }

                    }
                }
                return backupDir;
            }
            finally
            {
            }
        }

        /// <summary>
        /// Compact all items 
        /// </summary>
        public void Compact()
        {
            // Let other threads know they can't open a r/o connection for each db
            try
            {

                for (var i = 0; i < this.m_connections.Count; i++)
                {
                    var itm = this.m_connections.ElementAt(i);
                    var conn = this.GetConnection((ApplicationContext.Current.GetService<IConfigurationManager>()).GetConnectionString(itm.Key));
                    using (conn.Lock())
                    {
                        ApplicationContext.Current.SetProgress(Strings.locale_compacting, (i * 3 + 0) / (this.m_connections.Count * 3.0f));
                        conn.Execute("VACUUM");
                        ApplicationContext.Current.SetProgress(Strings.locale_compacting, (i * 3 + 1) / (this.m_connections.Count * 3.0f));
                        conn.Execute("REINDEX");
                        ApplicationContext.Current.SetProgress(Strings.locale_compacting, (i * 3 + 2) / (this.m_connections.Count * 3.0f));
                        conn.Execute("ANALYZE");
                    }
                }

            }
            finally
            {
            }
        }
    }

    /// <summary>
    /// Tracer trace listener
    /// </summary>
    internal class TracerTraceListener : ITraceListener
    {
        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteConnectionWithLock));

        /// <summary>
        /// Trace info to console
        /// </summary>
        public void Receive(string message)
        {
            this.m_tracer.TraceVerbose(message);
        }
    }
}
