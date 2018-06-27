using SanteDB.Core.Alerting;
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

namespace SanteDB.DisconnectedClient.SQLite.Alerting
{
    /// <summary>
    /// Alert persistence service
    /// </summary>
    public class AlertPersistenceService : IDataPersistenceService<AlertMessage>
    {
        public event EventHandler<DataPersistenceEventArgs<AlertMessage>> Inserted;
        public event EventHandler<DataPersistencePreEventArgs<AlertMessage>> Inserting;
        public event EventHandler<DataPersistenceEventArgs<AlertMessage>> Updated;
        public event EventHandler<DataPersistencePreEventArgs<AlertMessage>> Updating;
        public event EventHandler<DataPersistenceEventArgs<AlertMessage>> Obsoleted;
        public event EventHandler<DataPersistencePreEventArgs<AlertMessage>> Obsoleting;
        public event EventHandler<DataQueryEventArgsBase<AlertMessage>> Queried;
        public event EventHandler<DataQueryEventArgsBase<AlertMessage>> Querying;


        // Get tracer
        protected Tracer m_tracer; //= Tracer.GetTracer(typeof(LocalPersistenceServiceBase<TData>));

        // Configuration
        protected static DataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DataConfigurationSection>();

        // Mapper
        protected static ModelMapper m_mapper = new ModelMapper(typeof(AlertPersistenceService).GetTypeInfo().Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.SQLite.Alerting.ModelMap.xml"));

        // Builder
        protected static QueryBuilder m_builder;

        // Static CTOR
        static AlertPersistenceService()
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
            return new SQLiteDataContext(SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current.Configuration.GetConnectionString(m_configuration.MessageQueueConnectionStringName).Value));
        }

        /// <summary>
        /// Create readonly connection
        /// </summary>
        private SQLiteDataContext CreateReadonlyConnection()
        {
            return new SQLiteDataContext(SQLiteConnectionManager.Current.GetReadonlyConnection(ApplicationContext.Current.Configuration.GetConnectionString(m_configuration.MessageQueueConnectionStringName).Value));
        }

        /// <summary>
        /// Alert persistence ctor
        /// </summary>
        public AlertPersistenceService()
        {
            this.m_tracer = Tracer.GetTracer(this.GetType());
        }

        /// <summary>
        /// Count the number of alerts matching the query expression
        /// </summary>
        /// <param name="p">The query to filter the count</param>
        /// <returns>The total count of object matching the alert</returns>
        public int Count(Expression<Func<AlertMessage, bool>> p)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the alert message
        /// </summary>
        public AlertMessage Get(Guid key)
        {
            var idKey = key.ToByteArray();
            var conn = this.CreateReadonlyConnection();
            using (conn.LockConnection())
                return conn.Connection.Table<DbAlertMessage>().Where(o => o.Id == idKey).FirstOrDefault()?.ToAlert();
        }

        /// <summary>
        /// Insert the specified object
        /// </summary>
        public AlertMessage Insert(AlertMessage data)
        {
            var conn = this.CreateConnection();
            try
            {
                using (conn.LockConnection())
                {
                    if (!data.Key.HasValue) data.Key = Guid.NewGuid();
                    var dbData = new DbAlertMessage(data);
                
                    if(String.IsNullOrEmpty(dbData.CreatedBy))
                        dbData.CreatedBy = AuthenticationContext.Current.Principal?.Identity?.Name;

                    // Create table if not exists
                    if (!conn.Connection.TableMappings.Any(o => o.MappedType == typeof(DbAlertMessage)))
                    {
                        conn.Connection.CreateTable<DbAlertMessage>();
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
            return this.Insert((AlertMessage)data);
        }

        /// <summary>
        /// Obsolete an alert message
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public AlertMessage Obsolete(AlertMessage data)
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
        public IEnumerable<AlertMessage> Query(Expression<Func<AlertMessage, bool>> query)
        {
            int tr = 0;
            return this.Query(query, 0, 100, out tr, Guid.Empty);
        }

        /// <summary>
        /// Query for alerts with restrictions
        /// </summary>
        public IEnumerable<AlertMessage> Query(Expression<Func<AlertMessage, bool>> query, int offset, int? count, out int totalResults, Guid queryId)
        {
            try
            {
                var conn = this.CreateReadonlyConnection();
                using (conn.LockConnection())
                {
                    var dbPredicate = m_mapper.MapModelExpression<AlertMessage, DbAlertMessage>(query, false);

                    if (dbPredicate == null)
                    {
                        this.m_tracer.TraceError("Cannot map query to DB");
                        totalResults = 0;
                        return null;
                    }
                    else
                    {
                        var results = conn.Connection.Table<DbAlertMessage>().Where(dbPredicate).Skip(offset).Take(count ?? 100).OrderByDescending(o => o.TimeStamp).ToList().Select(o => o.ToAlert());
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
        public IEnumerable<AlertMessage> Query(string queryName, IDictionary<string, object> parameters)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Perform a stored query 
        /// </summary>
        public IEnumerable<AlertMessage> Query(string queryName, IDictionary<string, object> parameters, int offset, int? count, out int totalResults, Guid queryId)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Perform a generic query
        /// </summary>
        public IEnumerable Query(Expression query, int offset, int? count, out int totalResults)
        {
            return this.Query((Expression<Func<AlertMessage, bool>>)query, offset, count, out totalResults, Guid.Empty);
        }

        /// <summary>
        /// Query with explicit load
        /// </summary>
        public IEnumerable<AlertMessage> QueryExplicitLoad(Expression<Func<AlertMessage, bool>> query, int offset, int? count, out int totalResults, Guid queryId, IEnumerable<string> expandProperties)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Perform a fast query
        /// </summary>
        public IEnumerable<AlertMessage> QueryFast(Expression<Func<AlertMessage, bool>> query, int offset, int? count, out int totalResults, Guid queryId)
        {
            return this.Query(query, offset, count, out totalResults, Guid.Empty);
        }

        /// <summary>
        /// Update the alert message
        /// </summary>
        public AlertMessage Update(AlertMessage data)
        {
            var conn = this.CreateConnection();
            try
            {
                using (conn.LockConnection())
                {
                    if (!data.Key.HasValue)
                        throw new ArgumentException("Update object must have a key");

                    var dbData = new DbAlertMessage(data);

                    // Create table if not exists
                    if (!conn.Connection.TableMappings.Any(o => o.MappedType == typeof(DbAlertMessage)))
                        conn.Connection.CreateTable<DbAlertMessage>();

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
            return this.Update((AlertMessage)data);
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
