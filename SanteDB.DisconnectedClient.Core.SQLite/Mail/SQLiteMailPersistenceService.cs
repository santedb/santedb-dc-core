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
using SanteDB.DisconnectedClient.SQLite.Query;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Mail;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Mail.Hacks;
using SQLite.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.SQLite.Mail
{
    /// <summary>
    /// Alert persistence service
    /// </summary>
    public class SQLiteMailPersistenceService : IDataPersistenceService<MailMessage>
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "SQLite Mail Storage Service";

        public event EventHandler<DataPersistedEventArgs<MailMessage>> Inserted;
        public event EventHandler<DataPersistingEventArgs<MailMessage>> Inserting;
        public event EventHandler<DataPersistedEventArgs<MailMessage>> Updated;
        public event EventHandler<DataPersistingEventArgs<MailMessage>> Updating;
        public event EventHandler<DataPersistedEventArgs<MailMessage>> Obsoleted;
        public event EventHandler<DataPersistingEventArgs<MailMessage>> Obsoleting;
        public event EventHandler<QueryResultEventArgs<MailMessage>> Queried;
        public event EventHandler<QueryRequestEventArgs<MailMessage>> Querying;
        public event EventHandler<DataRetrievingEventArgs<MailMessage>> Retrieving;
        public event EventHandler<DataRetrievedEventArgs<MailMessage>> Retrieved;


        // Get tracer
        protected Tracer m_tracer; //= Tracer.GetTracer(typeof(LocalPersistenceServiceBase<TData>));

        // Configuration
        protected static DcDataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>();

        // Mapper
        protected static ModelMapper m_mapper = new ModelMapper(typeof(SQLiteMailPersistenceService).GetTypeInfo().Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.SQLite.Mail.ModelMap.xml"));

        // Builder
        protected static QueryBuilder m_builder;

        // Static CTOR
        static SQLiteMailPersistenceService()
        {
            m_builder = new QueryBuilder(m_mapper, new RecipientQueryHack());
        }

        /// <summary>
        /// Creates the connection.
        /// </summary>
        /// <returns>The connection.</returns>
        protected SQLiteDataContext CreateConnection()
        {
            return new SQLiteDataContext(SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(m_configuration.MailDataStore)), AuthenticationContext.SystemPrincipal);
        }

        /// <summary>
        /// Create readonly connection
        /// </summary>
        private SQLiteDataContext CreateReadonlyConnection()
        {
            return new SQLiteDataContext(SQLiteConnectionManager.Current.GetReadonlyConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(m_configuration.MailDataStore)), AuthenticationContext.SystemPrincipal);
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
        public long Count(Expression<Func<MailMessage, bool>> p, IPrincipal authContext = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the alert message
        /// </summary>
        public MailMessage Get(Guid key, Guid? versionKey, bool loadFast, IPrincipal authContext = null)
        {
            var idKey = key.ToByteArray();
            var conn = this.CreateReadonlyConnection();
            using (conn.LockConnection())
                return conn.Connection.Table<DbMailMessage>().Where(o => o.Id == idKey).FirstOrDefault()?.ToAlert();
        }

        /// <summary>
        /// Insert the specified object
        /// </summary>
        public MailMessage Insert(MailMessage data, TransactionMode mode, IPrincipal authContext)
        {
            var conn = this.CreateConnection();
            try
            {
                using (conn.LockConnection())
                {
                    if (!data.Key.HasValue) data.Key = Guid.NewGuid();

                    // Insert into each rcpt
                    foreach (var itm in data.RcptToXml.Distinct())
                    {
                        var dbData = new DbMailMessage(data);

                        // Route to proper RCPT TO
                        dbData.Recipient = itm.ToByteArray();

                        if (String.IsNullOrEmpty(dbData.CreatedBy))
                            dbData.CreatedBy = authContext?.Identity.Name ?? AuthenticationContext.Current.Principal?.Identity?.Name;

                        // Create table if not exists
                        if (!conn.Connection.TableMappings.Any(o => o.MappedType == typeof(DbMailMessage)))
                        {
                            conn.Connection.CreateTable<DbMailMessage>();
                        }

                        conn.Connection.Insert(dbData);
                    }
                    return data;
                }
            }
            catch (Exception e)
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
        public MailMessage Obsolete(MailMessage data, TransactionMode mode, IPrincipal authContext = null)
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
        public IEnumerable<MailMessage> Query(Expression<Func<MailMessage, bool>> query, IPrincipal authContext = null)
        {
            int tr = 0;
            return this.Query(query, 0, 100, out tr, authContext);
        }

        /// <summary>
        /// Query for alerts with restrictions
        /// </summary>
        public IEnumerable<MailMessage> Query(Expression<Func<MailMessage, bool>> query, int offset, int? count, out int totalResults, IPrincipal authContext, params ModelSort<MailMessage>[] orderBy)
        {
            return this.Query(query, offset, count, out totalResults, Guid.Empty, authContext, orderBy);
        }

        /// <summary>
        /// Query with query id
        /// </summary>
        /// <param name="query"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="totalResults"></param>
        /// <param name="queryId"></param>
        /// <param name=""></param>
        /// <param name="mode"></param>
        /// <param name="authContext"></param>
        /// <returns></returns>
        public IEnumerable<MailMessage> Query(Expression<Func<MailMessage, bool>> query, int offset, int? count, out int totalResults, Guid queryId, IPrincipal authContext, params ModelSort<MailMessage>[] orderBy)
        {
            try
            {
                var conn = this.CreateReadonlyConnection();
                using (conn.LockConnection())
                {

                    var dbPredicate = m_mapper.MapModelExpression<MailMessage, DbMailMessage, bool>(query, false);

                    IEnumerable<DbMailMessage> results = null;
                    if (dbPredicate == null)
                    {
                        var sqlStatement = m_builder.CreateQuery(query, orderBy).Build();
                        results = conn.Connection.DeferredQuery<DbMailMessage>(sqlStatement.SQL, sqlStatement.Arguments.ToArray());
                    }
                    else
                    {
                        var dbResults = conn.Connection.Table<DbMailMessage>().Where(dbPredicate);
                        if (orderBy != null && orderBy.Length > 0)
                        {
                            foreach (var itm in orderBy)
                                if (itm.SortOrder == SortOrderType.OrderBy)
                                    dbResults = dbResults.OrderBy(m_mapper.MapModelExpression<MailMessage, DbMailMessage, dynamic>(itm.SortProperty));
                                else
                                    dbResults = dbResults.OrderByDescending(m_mapper.MapModelExpression<MailMessage, DbMailMessage, dynamic>(itm.SortProperty));
                        }
                        else
                            dbResults = dbResults.OrderByDescending(o => o.TimeStamp);
                        results = dbResults;
                    }

                    totalResults = results.Count();
                   
                    return results.Skip(offset).Take(count ?? 100).ToList().Select(o => o.ToAlert());
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error searching alerts {0}: {1}", query, e);
                throw;
            }
        }

        /// <summary>
        /// Perform a generic query
        /// </summary>
        public IEnumerable Query(Expression query, int offset, int? count, out int totalResults)
        {
            return this.Query((Expression<Func<MailMessage, bool>>)query, offset, count, out totalResults, Guid.Empty, null);
        }

        /// <summary>
        /// Perform a fast query
        /// </summary>
        public IEnumerable<MailMessage> QueryFast(Expression<Func<MailMessage, bool>> query, int offset, int? count, out int totalResults, Guid queryId, IPrincipal authContext = null)
        {
            return this.Query(query, offset, count, out totalResults, Guid.Empty, null);
        }

        /// <summary>
        /// Update the alert message
        /// </summary>
        public MailMessage Update(MailMessage data, TransactionMode mode, IPrincipal authContext = null)
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


    }
}
