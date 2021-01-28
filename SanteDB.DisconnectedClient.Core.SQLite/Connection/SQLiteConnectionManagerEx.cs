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
 * Date: 2020-8-15
 */
using SanteDB.Core;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Services;
using SQLite.Net.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SanteDB.DisconnectedClient.SQLite.Connection
{
    /// <summary>
    /// Get the connection manager
    /// </summary>
    [ServiceProvider("SQLite Extended Connection Manager", Type = ServiceInstantiationType.Singleton)]
    public class SQLiteConnectionManager : IDataManagementService, IDiposable
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteConnectionManager));

        // Global lock object on the entire SQLiteConnection infrastructure
        private static object s_lockObject = new object();

        // The instance of the SQLite manager
        private static SQLiteConnectionManager s_instance;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "SQLite Extended Connection Manager";

        /// <summary>
        /// Connection pool
        /// </summary>
        private Dictionary<String, SQLiteConnectionPool> m_connectionPool = new Dictionary<String, SQLiteConnectionPool>();


        /// <summary>
        /// Gets the current connection manager
        /// </summary>
        public static SQLiteConnectionManager Current
        {
            get
            {
                if (s_instance == null)
                    lock (s_lockObject)
                        if (s_instance == null)
                            s_instance = new SQLiteConnectionManager();
                return s_instance;
            }
        }

        /// <summary>
        /// Create a new SQLite connection manager
        /// </summary>
        public SQLiteConnectionManager()
        {
            // Current instance should be disposed
            if (s_instance != null)
                s_instance.Dispose();

            s_instance = this;
        }

        /// <summary>
        /// Backup the database
        /// </summary>
        public string Backup(string passkey)
        {
            // Take the main connection lock
            lock (s_lockObject)
            {
                try
                {

                    var backupDir = Path.Combine(Path.GetTempPath(), "db-copy");
                    if (!Directory.Exists(backupDir))
                        Directory.CreateDirectory(backupDir);

                    ISQLitePlatform platform = ApplicationContext.Current.GetService<ISQLitePlatform>();
                    var connectionStrings = (ApplicationContext.Current.GetService<IConfigurationManager>()).GetSection<DcDataConfigurationSection>().ConnectionString;

                    for (var i = 0; i < connectionStrings.Count; i++)
                    {
                        var pool = this.GetConnectionPool(connectionStrings[i].Name);
                        //pool.CloseAll();
                        using (pool.Lock()) // Lock the pool
                        {
                            this.m_tracer.TraceInfo("Will backup {0} with passkey {1}", connectionStrings[i].GetComponent("dbfile"), !String.IsNullOrEmpty(passkey));
                            if (!File.Exists(connectionStrings[i].GetComponent("dbfile")))
                                continue;

                            var dataFile = connectionStrings[i].GetComponent("dbfile");

                            ApplicationContext.Current.SetProgress(Strings.locale_backup, (float)i / connectionStrings.Count);

                            var backupFile = Path.Combine(backupDir, Path.GetFileName(dataFile));
                            if (File.Exists(backupFile))
                                File.Delete(backupFile);
                            // Create empty database 
                            this.m_tracer.TraceVerbose("Creating temporary copy of database at {0}...", backupFile);

                            // Create new encrypted database
                            Mono.Data.Sqlite.SqliteConnection.CreateFile(backupFile);
                            using (var prodConn = new Mono.Data.Sqlite.SqliteConnection($"Data Source={backupFile}"))
                            {
                                prodConn.Open();
                                using (var cmd = prodConn.CreateCommand())
                                {
                                    cmd.CommandType = System.Data.CommandType.Text;
                                    cmd.CommandText = $"PRAGMA key = '{passkey}'";
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // Commands to be run
                            string[] sqlScript =
                            {
                        $"ATTACH DATABASE '{backupFile}' AS bak_db KEY '{passkey}'",
                        "SELECT sqlcipher_export('bak_db')",
                        "DETACH DATABASE bak_db"
                        };

                            // Attempt to use the existing security key 
                            using (var bakConn = new Mono.Data.Sqlite.SqliteConnection($"Data Source={dataFile}"))
                            {
                                if (connectionStrings[i].GetComponent("encrypt") == "true")
                                    bakConn.SetPassword(ApplicationContext.Current.GetCurrentContextSecurityKey());
                                bakConn.Open();
                                foreach (var sql in sqlScript)
                                    using (var cmd = bakConn.CreateCommand())
                                    {
                                        cmd.CommandType = System.Data.CommandType.Text;
                                        cmd.CommandText = sql;
                                        cmd.ExecuteNonQuery();

                                    }
                            }
                        }

                    }

                    return backupDir;
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException("Error backing up database", e);
                }
            }
        }

        /// <summary>
        /// Compact the database
        /// </summary>
        public void Compact()
        {
            // Let other threads know they can't open a r/o connection for each db
            try
            {
                var i = 0;
                foreach (var itm in this.m_connectionPool)
                {
                    i++;
                    using (itm.Value.Lock()) // prevent other threads
                    {
                        this.m_tracer.TraceVerbose("Closing existing connections to {0}...", itm.Key);
                        itm.Value.CloseAll();
                        var cstr = (ApplicationContext.Current.GetService<IConfigurationManager>()).GetConnectionString(itm.Key);
                        var conn = this.GetReadWriteConnection(cstr);
                        using (conn.Lock())
                        {
                            this.m_tracer.TraceVerbose("VACUUM / REINDEX / ANALYZE {0}...", itm.Key);
                            ApplicationContext.Current.SetProgress(Strings.locale_compacting, (i * 3 + 0) / (this.m_connectionPool.Count * 3.0f));
                            conn.Execute("VACUUM");
                            ApplicationContext.Current.SetProgress(Strings.locale_compacting, (i * 3 + 1) / (this.m_connectionPool.Count * 3.0f));
                            conn.Execute("REINDEX");
                            ApplicationContext.Current.SetProgress(Strings.locale_compacting, (i * 3 + 2) / (this.m_connectionPool.Count * 3.0f));
                            conn.Execute("ANALYZE");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error compacting database", e);
            }
        }

        /// <summary>
        /// Rekey databases
        /// </summary>
        public void RekeyDatabases()
        {
            if (ApplicationContext.Current.GetCurrentContextSecurityKey() == null) return; // no need to rekey

            lock (s_lockObject) // prevent others from obtaining connections
                try
                {

                    ISQLitePlatform platform = ApplicationContext.Current.GetService<ISQLitePlatform>();
                    var connectionStrings = (ApplicationContext.Current.GetService<IConfigurationManager>()).GetSection<DcDataConfigurationSection>().ConnectionString;
                    for (var i = 0; i < connectionStrings.Count; i++)
                    {
                        ApplicationContext.Current.SetProgress(Strings.locale_backup_restore, (float)i / connectionStrings.Count);
                        var cstr = connectionStrings[i];

                        // Obtain and lock the connection pool
                        this.m_tracer.TraceVerbose("Rekey {0} - Closing existing connections", cstr.Name);
                        var pool = this.GetConnectionPool(cstr.Name);
                        pool.CloseAll();

                        using (pool.Lock()) // Prevent new pool connections from being created
                        {
                            var dbFile = cstr.GetComponent("dbfile");
                            try
                            {
                                if (!File.Exists(dbFile)) continue; // could not find file
                                
                                File.Move(dbFile, Path.ChangeExtension(dbFile, "old"));

                                // Create new encrypted database
                                Mono.Data.Sqlite.SqliteConnection.CreateFile(dbFile);
                                using (var prodConn = new Mono.Data.Sqlite.SqliteConnection($"Data Source={dbFile}"))
                                {
                                    prodConn.Open();
                                    using (var cmd = prodConn.CreateCommand())
                                    {
                                        cmd.CommandType = System.Data.CommandType.Text;
                                        cmd.CommandText = $"PRAGMA key = '{Encoding.UTF8.GetString(ApplicationContext.Current.GetCurrentContextSecurityKey())}'";
                                        cmd.ExecuteNonQuery();
                                    }
                                }

                                // Commands to be run
                                string[] sqlScript =
                                {
                                    $"ATTACH DATABASE '{dbFile}' AS prod_db KEY '{Encoding.UTF8.GetString(ApplicationContext.Current.GetCurrentContextSecurityKey())}'",
                                    "SELECT sqlcipher_export('prod_db')",
                                    "DETACH DATABASE prod_db"
                                };

                                // Attempt to use the existing security key 
                                using (var bakConn = new Mono.Data.Sqlite.SqliteConnection($"Data Source={Path.ChangeExtension(dbFile, "old")}"))
                                {
                                    bakConn.Open();
                                    foreach (var sql in sqlScript)
                                        using (var cmd = bakConn.CreateCommand())
                                        {
                                            cmd.CommandType = System.Data.CommandType.Text;
                                            cmd.CommandText = sql;
                                            cmd.ExecuteNonQuery();

                                        }
                                }
                            }
                            finally
                            {
                                File.Delete(Path.ChangeExtension(dbFile, "old"));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error rekeying database: {0}", e);
                    throw new Exception("Error re-keying databases", e);
                }
        }

        /// <summary>
        /// Connection string get readonly
        /// </summary>
        public LockableSQLiteConnection GetReadonlyConnection(ConnectionString dataSource)
        {
            var conn = this.GetOrCreatePooledConnection(dataSource, true);
#if DEBUG_SQL
            conn.TraceListener = new TracerTraceListener();
#endif
            return conn;
        }

        /// <summary>
        /// Get read/write connection
        /// </summary>
        public LockableSQLiteConnection GetReadWriteConnection(ConnectionString dataSource)
        {
            var conn = this.GetOrCreatePooledConnection(dataSource, false);
#if DEBUG_SQL
            conn.TraceListener = new TracerTraceListener();
#endif
            return conn;
        }

        /// <summary>
        /// Get the connection pool
        /// </summary>
        private SQLiteConnectionPool GetConnectionPool(String poolName)
        {
            lock (s_lockObject)
            {
                if (!this.m_connectionPool.TryGetValue(poolName, out SQLiteConnectionPool connectionPool))

                    // After obtaining lock, the connection pool still doesn't contain connection 
                    if (!this.m_connectionPool.ContainsKey(poolName))
                    {
                        connectionPool = new SQLiteConnectionPool();
                        this.m_connectionPool.Add(poolName, connectionPool);
                    }
                return connectionPool;
            }
        }

        /// <summary>
        /// Get the specified connection from the pool or create one
        /// </summary>
        private LockableSQLiteConnection GetOrCreatePooledConnection(ConnectionString dataSource, bool isReadonly)
        {

            // Pool exists?
            var connectionPool = this.GetConnectionPool(dataSource.Name);

            // Get the platform
            ISQLitePlatform platform = ApplicationServiceContext.Current.GetService<ISQLitePlatform>();

            // Try to lock global (make sure another process isn't hogging it)

            LockableSQLiteConnection retVal = null;
            if (isReadonly) // Readonly we can use any connection we like
            {
                retVal = connectionPool.GetEntered() ??
                    connectionPool.GetFree();
                if (retVal == null) // Create a connection 
                {
                    retVal = new LockableSQLiteConnection(platform, dataSource, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex );
                    retVal.Execute("PRAGMA synchronous = 1");
                    connectionPool.Add(retVal);
                    //retVal.Wait();
                }
                else
                    retVal.Wait(); // Wait for connection to become available

            }
            else // We want write
            {
                retVal = connectionPool.GetWritable(); // Might be writable connection available 
                if (retVal == null)
                {
                    retVal = new LockableSQLiteConnection(platform, dataSource, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.Create);
                    retVal.Execute("PRAGMA synchronous = 1");
                    retVal = connectionPool.AddUnique(retVal, o => !o.IsReadonly);
                    retVal.Wait();
                }
                else  // Wait for the connection to become available
                    retVal.Wait();

            }

            return retVal;
        }

        /// <summary>
        /// Get a lock for the specified database object
        /// </summary>
        public IDisposable ExternLock(string dsName)
        {

            var connectionPool = this.GetConnectionPool(dsName);
            var retVal = connectionPool.Lock(); // Get exclusive lock on the connection pool
            return retVal;

        }

        /// <summary>
        /// Dispose of the object
        /// </summary>
        public void Dispose()
        {
            foreach (var itm in this.m_connectionPool)
                itm.Value.Dispose();
            this.m_connectionPool.Clear();
        }

    }
}
