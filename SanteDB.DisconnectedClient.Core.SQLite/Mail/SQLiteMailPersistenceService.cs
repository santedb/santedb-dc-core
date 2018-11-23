/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-7-31
 */
using SanteDB.Core.Mail;
using SanteDB.Core.Data.QueryBuilder;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.SQLite.Connection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Mail
{
    /// <summary>
    /// Alert persistence service
    /// </summary>
    public class SQLiteMailPersistenceService : IDataPersistenceService<MailMessage>
    {
        public event EventHandler<DataPersistenceEventArgs<MailMessage>> Inserted;
        public event EventHandler<DataPersistencePreEventArgs<MailMessage>> Inserting;
        public event EventHandler<DataPersistenceEventArgs<MailMessage>> Updated;
        public event EventHandler<DataPersistencePreEventArgs<MailMessage>> Updating;
        public event EventHandler<DataPersistenceEventArgs<MailMessage>> Obsoleted;
        public event EventHandler<DataPersistencePreEventArgs<MailMessage>> Obsoleting;
        public event EventHandler<DataQueryEventArgsBase<MailMessage>> Queried;
        public event EventHandler<DataQueryEventArgsBase<MailMessage>> Querying;


        // Get tracer
        protected Tracer m_tracer; //= Tracer.GetTracer(typeof(LocalPersistenceServiceBase<TData>));

        // Configuration
        protected static DataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DataConfigurationSection>();

        // Mapper
        protected static ModelMapper m_mapper = new ModelMapper(typeof(SQLiteMailPersistenceService).GetTypeInfo().Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.SQLite.Mail.ModelMap.xml"));

        // Builder
        protected static QueryBuilder m_builder;

        // Static CTOR
        static SQLiteMailPersistenceService()
        {
            m_mapper = SQLitePersistenceService.Mapper;
            m_builder = new QueryBuilder(m_mapper);
        }

        /// <summary>
        /// Creates the connection.
        /// </summary>
        /// <returns>The connection.</returns>
        protected SQLiteDataContext CreateConnection()
        {
            return new SQLiteDataContext(SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current.Configuration.GetConnectionString(m_configuration.MailDataStore).Value));
        }

        /// <summary>
        /// Create readonly connection
        /// </summary>
        private SQLiteDataContext CreateReadonlyConnection()
        {
            return new SQLiteDataContext(SQLiteConnectionManager.Current.GetReadonlyConnection(ApplicationContext.Current.Configuration.GetConnectionString(m_configuration.MailDataStore).Value));
        }

        /// <summary>
        /// Alert persistence ctor
        /// </summary>
        public SQLiteMailPersistenceService()
        {
            this.m_tracer = Tracer.GetTracer(this.GetType());
        }

        /// <summary>
        /// Count the number of alerts matching the query expression
        /// </summary>
        /// <param name="p">The query to filter the count</param>
        /// <returns>The total count of object matching the alert</returns>
        public int Count(Expression<Func<MailMessage, bool>> p)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the alert message
        /// </summary>
        public MailMessage Get(Guid key)
        {
            var idKey = key.ToByteArray();
            var conn = this.CreateReadonlyConnection();
            using (conn.LockConnection())
                return conn.Connection.Table<DbMailMessage>().Where(o => o.Id == idKey).FirstOrDefault()?.ToAlert();
        }

        /// <summary>
        /// Insert the specified object
        /// </summary>
        public MailMessage Insert(MailMessage data)
        {
            var conn = this.CreateConnection();
            try
            {
                using (conn.LockConnection())
                {
                    if (!data.Key.HasValue) data.Key = Guid.NewGuid();
                    var dbData = new DbMailMessage(data);
                
                    if(String.IsNullOrEmpty(dbData.CreatedBy))
                        dbData.CreatedBy = AuthenticationContext.Current.Principal?.Identity?.Name;

                    // Create table if not exists
                    if (!conn.Connection.TableMappings.Any(o => o.MappedType == typeof(DbMailMessage)))
                    {
                        conn.Connection.CreateTable<DbMailMessage>();
                    }

                    conn.Connection.Insert(dbData);

                    return data;
                }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Error inserting alert message: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Generic version of insert
        /// </summary>
        public object Insert(object data)
        {
            return this.Insert((MailMessage)data);
        }

        /// <summary>
        /// Obsolete an alert message
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public MailMessage Obsolete(MailMessage data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Obsoletes an alert message
        /// </summary>
        public object Obsolete(object data)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Query for alerts
        /// </summary>
        public IEnumerable<MailMessage> Query(Expression<Func<MailMessage, bool>> query)
        {
            int tr = 0;
            return this.Query(query, 0, 100, out tr, Guid.Empty);
        }

        /// <summary>
        /// Query for alerts with restrictions
        /// </summary>
        public IEnumerable<MailMessage> Query(Expression<Func<MailMessage, bool>> query, int offset, int? count, out int totalResults, Guid queryId)
        {
            try
            {
                var conn = this.CreateReadonlyConnection();
                using (conn.LockConnection())
                {
                    var dbPredicate = m_mapper.MapModelExpression<MailMessage, DbMailMessage>(query, false);

                    if (dbPredicate == null)
                    {
                        this.m_tracer.TraceError("Cannot map query to DB");
                        totalResults = 0;
                        return null;
                    }
                    else
                    {
                        var results = conn.Connection.Table<DbMailMessage>().Where(dbPredicate).Skip(offset).Take(count ?? 100).OrderByDescending(o => o.TimeStamp).ToList().Select(o => o.ToAlert());
                        totalResults = results.Count();
                        return results;
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error searching alerts {0}: {1}", query, e);
                throw;
            }
        }

        /// <summary>
        /// Perform a stored query 
        /// </summary>
        public IEnumerable<MailMessage> Query(string queryName, IDictionary<string, object> parameters)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Perform a stored query 
        /// </summary>
        public IEnumerable<MailMessage> Query(string queryName, IDictionary<string, object> parameters, int offset, int? count, out int totalResults, Guid queryId)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Perform a generic query
        /// </summary>
        public IEnumerable Query(Expression query, int offset, int? count, out int totalResults)
        {
            return this.Query((Expression<Func<MailMessage, bool>>)query, offset, count, out totalResults, Guid.Empty);
        }

        /// <summary>
        /// Query with explicit load
        /// </summary>
        public IEnumerable<MailMessage> QueryExplicitLoad(Expression<Func<MailMessage, bool>> query, int offset, int? count, out int totalResults, Guid queryId, IEnumerable<string> expandProperties)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Perform a fast query
        /// </summary>
        public IEnumerable<MailMessage> QueryFast(Expression<Func<MailMessage, bool>> query, int offset, int? count, out int totalResults, Guid queryId)
        {
            return this.Query(query, offset, count, out totalResults, Guid.Empty);
        }

        /// <summary>
        /// Update the alert message
        /// </summary>
        public MailMessage Update(MailMessage data)
        {
            var conn = this.CreateConnection();
            try
            {
                using (conn.LockConnection())
                {
                    if (!data.Key.HasValue)
                        throw new ArgumentException("Update object must have a key");

                    var dbData = new DbMailMessage(data);

                    // Create table if not exists
                    if (!conn.Connection.TableMappings.Any(o => o.MappedType == typeof(DbMailMessage)))
                        conn.Connection.CreateTable<DbMailMessage>();

                    conn.Connection.Update(dbData);

                    return data;
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error updating alert message: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Update the specified data
        /// </summary>
        public object Update(object data)
        {
            return this.Update((MailMessage)data);
        }

        /// <summary>
        /// Get the specified object
        /// </summary>
        object IDataPersistenceService.Get(Guid id)
        {
            return this.Get(id);
        }
    }
}
