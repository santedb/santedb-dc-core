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
using SanteDB.Core.Security;
using SanteDB.Core.Auditing;
using SanteDB.Core.Data.QueryBuilder;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Security.Audit.Model;
using SQLite.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SanteDB.Core.Model.Query;

namespace SanteDB.DisconnectedClient.Core.Security.Audit
{
    /// <summary>
    /// Local audit repository service
    /// </summary>
    public class SQLiteAuditRepositoryService : IAuditRepositoryService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "SQLite Audit Repository Service";

        // Model mapper
        private ModelMapper m_mapper = new ModelMapper(typeof(SQLiteAuditRepositoryService).GetTypeInfo().Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.SQLite.Security.Audit.Model.ModelMap.xml"));

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteAuditRepositoryService));

        /// <summary>
        /// Ctor (prune on startup)
        /// </summary>
        public SQLiteAuditRepositoryService()
        {
            ApplicationContext.Current.Started += (o, e) =>
            {
                try
                {
                    this.Prune();
                }
                catch (Exception ex)
                {
                    this.m_tracer.TraceError("Error pruning audit repository: {0}", ex);
                }
            };
        }

        /// <summary>
        /// Create a connection
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateConnection()
        {
            return SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(
                "santeDbAudit"
            ));
        }

        /// <summary>
        /// Prune the audit database
        /// </summary>
        public void Prune()
        {
            var config = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            try
            {
                this.m_tracer.TraceInfo("Prune audits older than {0}", config?.AuditRetention);
                if (config?.AuditRetention == null) return; // keep audits forever

                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    try
                    {
                        conn.BeginTransaction();
                        DateTime cutoff = DateTime.Now.Subtract(config.AuditRetention);
                        Expression<Func<DbAuditData, bool>> epred = o => o.CreationTime < cutoff;
                        conn.Table<DbAuditData>().Delete(epred);

                        // Delete objects
                        conn.Execute($"DELETE FROM {conn.GetMapping<DbAuditObject>().TableName} WHERE NOT({conn.GetMapping<DbAuditObject>().FindColumnWithPropertyName(nameof(DbAuditObject.AuditId)).Name} IN " +
                            $"(SELECT {conn.GetMapping<DbAuditData>().FindColumnWithPropertyName(nameof(DbAuditData.Id)).Name} FROM {conn.GetMapping<DbAuditData>().TableName})" +
                            ")");

                        AuditUtil.AuditAuditLogUsed(ActionType.Delete, OutcomeIndicator.Success, epred.ToString());
                        conn.Commit();
                    }
                    catch
                    {
                        conn.Rollback();
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error pruning audit database: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Finds the specified audit in the data repository
        /// </summary>
        public IEnumerable<AuditData> Find(Expression<Func<AuditData, bool>> query)
        {
            int tr = 0;
            return this.Find(query, 0, null, out tr, null);
        }

        /// <summary>
        /// Get the specified audit data from the database
        /// </summary>
        public AuditData Get(object id)
        {
            try
            {
                Guid pk = Guid.Empty;
                if (id is Guid)
                    pk = (Guid)id;
                else if (id is String)
                    pk = Guid.Parse(id.ToString());
                else
                    throw new ArgumentException("Parameter must be GUID or parsable as GUID", nameof(id));

                // Fetch 
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var builder = new QueryBuilder(this.m_mapper);
                    var sql = builder.CreateQuery<AuditData>(o => o.Key == pk, null).Limit(1).Build();

                    var res = conn.Query<DbAuditData.QueryResult>(sql.SQL, sql.Arguments.ToArray()).FirstOrDefault();
                    AuditUtil.AuditAuditLogUsed(ActionType.Read, OutcomeIndicator.Success, sql.ToString(), pk);

                    return this.ToModelInstance(conn, res, false);
                }
            }
            catch (Exception e)
            {
                AuditUtil.AuditAuditLogUsed(ActionType.Read, OutcomeIndicator.EpicFail, id.ToString());
                this.m_tracer.TraceError("Error retrieving audit {0} : {1}", id, e);
                throw;
            }
        }

        /// <summary>
        /// Convert a db audit to model 
        /// </summary>
        private AuditData ToModelInstance(SQLiteConnection context, DbAuditData.QueryResult res, bool summary = true)
        {
            var retVal = new AuditData()
            {
                ActionCode = (ActionType)res.ActionCode,
                EventIdentifier = (EventIdentifierType)res.EventIdentifier,
                Outcome = (OutcomeIndicator)res.Outcome,
                Timestamp = res.Timestamp,
                Key = new Guid(res.Id)
            };

            if (res.EventTypeCode != null)
            {
                var concept = ApplicationContext.Current.GetService<IConceptRepositoryService>().GetConcept(res.Code);
                retVal.EventTypeCode = new AuditCode(res.Code, res.CodeSystem)
                {
                    DisplayName = concept?.ConceptNames.First()?.Name ?? res.Code
                };
            }

            // Get actors and objects
            if (!summary)
            {

                // Actors
                var sql = new SqlStatement<DbAuditActorAssociation>().SelectFrom()
                        .InnerJoin<DbAuditActorAssociation, DbAuditActor>(o => o.TargetUuid, o => o.Id)
                        .Join<DbAuditActor, DbAuditCode>("LEFT", o => o.ActorRoleCode, o => o.Id)
                        .Where<DbAuditActorAssociation>(o => o.SourceUuid == res.Id)
                        .Build();

                foreach (var itm in context.Query<DbAuditActor.QueryResult>(sql.SQL, sql.Arguments.ToArray()))
                    retVal.Actors.Add(new AuditActorData()
                    {
                        UserName = itm.UserName,
                        UserIsRequestor = itm.UserIsRequestor,
                        UserIdentifier = itm.UserIdentifier,
                        ActorRoleCode = new List<AuditCode>()
                        {
                            new AuditCode(itm.Code, itm.CodeSystem)
                        }
                    });

                // Objects
                foreach (var itm in context.Table<DbAuditObject>().Where(o => o.AuditId == res.Id))
                {
                    retVal.AuditableObjects.Add(new AuditableObject()
                    {
                        IDTypeCode = (AuditableObjectIdType?)itm.IDTypeCode,
                        LifecycleType = (AuditableObjectLifecycle?)itm.LifecycleType,
                        NameData = itm.NameData,
                        ObjectId = itm.ObjectId,
                        QueryData = itm.QueryData,
                        Role = (AuditableObjectRole?)itm.Role,
                        Type = (AuditableObjectType)itm.Type
                    });
                }
            }
            else
            {
                // Actors
                var sql = new SqlStatement<DbAuditActorAssociation>().SelectFrom()
                        .InnerJoin<DbAuditActorAssociation, DbAuditActor>(o => o.TargetUuid, o => o.Id)
                        .Join<DbAuditActor, DbAuditCode>("LEFT", o => o.ActorRoleCode, o => o.Id)
                        .Where<DbAuditActorAssociation>(o => o.SourceUuid == res.Id).And<DbAuditActor>(p => p.UserIsRequestor == true)
                        .Build();

                foreach (var itm in context.Query<DbAuditActor.QueryResult>(sql.SQL, sql.Arguments.ToArray()))
                    retVal.Actors.Add(new AuditActorData()
                    {
                        UserName = itm.UserName,
                        UserIsRequestor = itm.UserIsRequestor,
                        UserIdentifier = itm.UserIdentifier,
                        ActorRoleCode = new List<AuditCode>()
                        {
                            new AuditCode(itm.Code, itm.CodeSystem)
                        }
                    });
            }

            return retVal;
        }

        /// <summary>
        /// Fids the specified audit in the local repository
        /// </summary>
        public IEnumerable<AuditData> Find(Expression<Func<AuditData, bool>> query, int offset, int? count, out int totalResults, params ModelSort<AuditData>[] orderBy)
        {
            try
            {
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var builder = new QueryBuilder(this.m_mapper);
                    var sql = builder.CreateQuery(query, orderBy).Build();

                    if(orderBy == null || orderBy.Length == 0)
                        sql = sql.OrderBy<DbAuditData>(o => o.Timestamp, SortOrderType.OrderByDescending);

                    // Total results
                    totalResults = conn.ExecuteScalar<Int32>($"SELECT COUNT(*) FROM ({sql.SQL})", sql.Arguments.ToArray());

                    // Query control
                    if (count.HasValue)
                        sql.Limit(count.Value);
                    if (offset > 0)
                    {
                        if (count == 0)
                            sql.Limit(100).Offset(offset);
                        else
                            sql.Offset(offset);
                    }
                    sql = sql.Build();
                    var itm = conn.Query<DbAuditData.QueryResult>(sql.SQL, sql.Arguments.ToArray());
                    AuditUtil.AuditAuditLogUsed(ActionType.Read, OutcomeIndicator.Success, sql.ToString(), itm.Select(o => new Guid(o.Id)).ToArray());
                    return itm.Select(o => this.ToModelInstance(conn, o)).ToList();
                }
            }
            catch (Exception e)
            {
                AuditUtil.AuditAuditLogUsed(ActionType.Read, OutcomeIndicator.EpicFail, query.ToString());
                this.m_tracer.TraceError("Could not query audit {0}: {1}", query, e);
                throw;
            }
        }

        /// <summary>
        /// Insert audit data
        /// </summary>
        public AuditData Insert(AuditData audit)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    if(!conn.IsInTransaction)
                        conn.BeginTransaction();

                    // Insert core
                    var dbAudit = this.m_mapper.MapModelInstance<AuditData, DbAuditData>(audit);

                    var eventId = audit.EventTypeCode;
                    if (eventId != null)
                    {
                        var existing = conn.Table<DbAuditCode>().Where(o => o.Code == eventId.Code && o.CodeSystem == eventId.CodeSystem).FirstOrDefault();
                        if (existing == null)
                        {
                            Guid codeId = Guid.NewGuid();
                            dbAudit.EventTypeCode = codeId.ToByteArray();
                            conn.Insert(new DbAuditCode() { Code = eventId.Code, CodeSystem = eventId.CodeSystem, Id = codeId.ToByteArray() });
                        }
                        else
                            dbAudit.EventTypeCode = existing.Id;
                    }

                    dbAudit.CreationTime = DateTime.Now;
                    audit.Key = Guid.NewGuid();
                    dbAudit.Id = audit.Key.Value.ToByteArray();
                    conn.Insert(dbAudit);

                    // Insert secondary properties
                    if (audit.Actors != null)
                        foreach (var act in audit.Actors)
                        {
                            var dbAct = conn.Table<DbAuditActor>().Where(o => o.UserName == act.UserName).FirstOrDefault();
                            if (dbAct == null)
                            {
                                dbAct = this.m_mapper.MapModelInstance<AuditActorData, DbAuditActor>(act);
                                dbAct.Id = Guid.NewGuid().ToByteArray();
                                conn.Insert(dbAct);
                                var roleCode = act.ActorRoleCode?.FirstOrDefault();
                                if (roleCode != null)
                                {
                                    var existing = conn.Table<DbAuditCode>().Where(o => o.Code == roleCode.Code && o.CodeSystem == roleCode.CodeSystem).FirstOrDefault();
                                    if (existing == null)
                                    {
                                        dbAct.ActorRoleCode = Guid.NewGuid().ToByteArray();
                                        conn.Insert(new DbAuditCode() { Code = roleCode.Code, CodeSystem = roleCode.CodeSystem, Id = dbAct.ActorRoleCode });
                                    }
                                    else
                                        dbAct.ActorRoleCode = existing.Id;
                                }

                            }
                            conn.Insert(new DbAuditActorAssociation()
                            {
                                TargetUuid = dbAct.Id,
                                SourceUuid = dbAudit.Id,
                                Id = Guid.NewGuid().ToByteArray()
                            });
                        }

                    // Audit objects
                    if (audit.AuditableObjects != null)
                        foreach (var ao in audit.AuditableObjects)
                        {
                            var dbAo = this.m_mapper.MapModelInstance<AuditableObject, DbAuditObject>(ao);
                            dbAo.IDTypeCode = (int)(ao.IDTypeCode ?? 0);
                            dbAo.LifecycleType = (int)(ao.LifecycleType ?? 0);
                            dbAo.Role = (int)(ao.Role ?? 0);
                            dbAo.Type = (int)(ao.Type);
                            dbAo.AuditId = dbAudit.Id;
                            dbAo.Id = Guid.NewGuid().ToByteArray();
                            conn.Insert(dbAo);
                        }

                    conn.Commit();

                    return audit;
                }
                catch (Exception ex)
                {
                    conn.Rollback();
                    this.m_tracer.TraceError("Error inserting audit: {0}", ex);
                    throw;
                }
            }
        }
    }
}
