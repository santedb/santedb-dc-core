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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Synchronization.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.SQLite.Synchronization
{
    /// <summary>
    /// Represents the synchronization log
    /// </summary>
    public class SQLiteSynchronizationLog : ISynchronizationLogService
    {
        // Get the tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteSynchronizationLog));

        // Object sync
        private object m_syncObject = new object();

        // The log instance
        private static SQLiteSynchronizationLog s_instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.SQLite.Synchronization.SynchronizationQueue`1"/> class.
        /// </summary>
        public SQLiteSynchronizationLog()
        {
        }

        /// <summary>
        /// Create a connection
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateConnection()
        {
            return SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(
                ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>().MessageQueueConnectionStringName
            ));
        }

        /// <summary>
        /// Get the last successful modification time of an object retrieved
        /// </summary>
        public DateTime? GetLastTime(Type modelType, String filter = null)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                var modelAqn = modelType.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>().TypeName;
                var logEntry = conn.Table<SynchronizationLogEntry>().Where(o => o.ResourceType == modelAqn && o.Filter == filter).FirstOrDefault();
                return logEntry?.LastSync.ToLocalTime();
            }
        }

        /// <summary>
        /// Get the last successful etag
        /// </summary>
        public String GetLastEtag(Type modelType, String filter = null)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                var modelAqn = modelType.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>().TypeName;
                var logEntry = conn.Table<SynchronizationLogEntry>().Where(o => o.ResourceType == modelAqn && o.Filter == filter).FirstOrDefault();
                return logEntry?.LastETag;
            }
        }

        /// <summary>
        /// Save the sync log entry
        /// </summary>
        public void Save(Type modelType, String filter, String eTag, String name, DateTime since)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                var modelAqn = modelType.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>().TypeName;
                var logEntry = conn.Table<SynchronizationLogEntry>().Where(o => o.ResourceType == modelAqn && o.Filter == filter).FirstOrDefault();
                if (logEntry == null)
                    conn.Insert(new SynchronizationLogEntry() { ResourceType = modelAqn, Filter = filter, LastETag = eTag, LastSync = since });
                else
                {
                    logEntry.LastSync = since;
                    if (!String.IsNullOrEmpty(eTag))
                        logEntry.LastETag = eTag;
                    conn.Update(logEntry);
                }
            }
        }

        /// <summary>
        /// Get all synchronizations
        /// </summary>
        public List<ISynchronizationLogEntry> GetAll()
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                return conn.Table<SynchronizationLogEntry>().OfType<ISynchronizationLogEntry>().ToList();
            }
        }

        /// <summary>
        /// Save the query state so that it can come back if the connection is lost
        /// </summary>
        public void SaveQuery(Type modelType, String filter, Guid queryId, String name, int offset)
        {

            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    var qid = queryId.ToByteArray();
                    var currentQuery = conn.Table<SynchronizationQuery>().Where(o => o.Uuid == qid).FirstOrDefault();
                    if (currentQuery == null)
                    {
                        var modelAqn = modelType.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>().TypeName;

                        conn.Insert(new SynchronizationQuery()
                        {
                            Uuid = queryId.ToByteArray(),
                            Filter = filter,
                            LastSuccess = offset,
                            StartTime = DateTime.Now,
                            ResourceType = modelAqn
                        });
                    }
                    else
                    {
                        currentQuery.LastSuccess = offset;
                        conn.Update(currentQuery);
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error saving query data {0} : {1}", queryId, e);
                    throw;
                }
            }

        }

        /// <summary>
        /// Complete query 
        /// </summary>
        public void CompleteQuery(Guid queryId)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    var qid = queryId.ToByteArray();
                    conn.Table<SynchronizationQuery>().Delete(o => o.Uuid == qid);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error deleting query data {0} : {1}", queryId, e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Find query data
        /// </summary>
        public ISynchronizationLogQuery FindQueryData(Type modelType, String filter)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    var modelAqn = modelType.GetTypeInfo().GetCustomAttribute<XmlTypeAttribute>().TypeName;
                    return conn.Table<SynchronizationQuery>().Where(o => o.ResourceType == modelAqn && o.Filter == filter).FirstOrDefault();
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error fetching query data {0} : {1}", modelType, e);
                    throw;
                }
            }
        }


        /// <summary>
        /// Delete the specified synchronization log entry
        /// </summary>
        public void Delete(ISynchronizationLogEntry itm)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    conn.Table<SynchronizationLogEntry>().Delete(o => o.ResourceType == itm.ResourceType && o.Filter == itm.Filter);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error removing query data {0} : {1}", itm.ResourceType, e);
                    throw;
                }
            }
        }
    }
}
