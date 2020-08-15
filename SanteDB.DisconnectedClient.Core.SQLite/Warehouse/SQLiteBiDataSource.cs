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
 * Date: 2019-12-24
 */
using Mono.Data.Sqlite;
using SanteDB.BI;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.BI.Util;
using SanteDB.Core;
using SanteDB.Core.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Query;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SQLite.Net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SanteDB.DisconnectedClient.SQLite.Warehouse
{
    /// <summary>
    /// A report source which uses SQLite
    /// </summary>
    public class SQLiteBiDataSource : IBiDataSource
    {
        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteBiDataSource));

        // Epoch
        private readonly DateTime m_epoch = new DateTime(621355968000000000, DateTimeKind.Utc);

        /// <summary>
        /// Create a command with the specified parameters
        /// </summary>
        private IDbCommand CreateCommand(IDbConnection connection, string sql, params object[] parameters)
        {

            var retVal = connection.CreateCommand();
            retVal.CommandType = System.Data.CommandType.Text;
            retVal.CommandText = sql;
            foreach (var itm in parameters)
            {
                var parm = retVal.CreateParameter();
                parm.Value = itm;
                if (itm is String) parm.DbType = System.Data.DbType.String;
                else if (itm is DateTime || itm is DateTimeOffset)
                {
                    parm.DbType = System.Data.DbType.Int64;
                    if (itm is DateTime)
                    {
                        DateTime dt = (DateTime)itm;
                        switch (dt.Kind)
                        {
                            case DateTimeKind.Local:
                                parm.Value = ((DateTime)itm).ToUniversalTime().Subtract(this.m_epoch).TotalSeconds;
                                break;
                            default:
                                parm.Value = ((DateTime)itm).Subtract(this.m_epoch).TotalSeconds;
                                break;
                        }
                    }
                    else
                        parm.Value = ((DateTimeOffset)itm).ToUniversalTime().Subtract(this.m_epoch).TotalSeconds;
                }
                else if (itm is Int32) parm.DbType = System.Data.DbType.Int32;
                else if (itm is Boolean) parm.DbType = System.Data.DbType.Boolean;
                else if (itm is byte[])
                {
                    parm.DbType = System.Data.DbType.Binary;
                    parm.Value = itm;
                }
                else if (itm is Guid || itm is Guid?)
                {
                    parm.DbType = System.Data.DbType.Binary;
                    if (itm != null)
                        parm.Value = ((Guid)itm).ToByteArray();
                    else parm.Value = DBNull.Value;
                }
                else if (itm is float || itm is double) parm.DbType = System.Data.DbType.Double;
                else if (itm is Decimal) parm.DbType = System.Data.DbType.Decimal;
                else if (itm == null)
                {
                    parm.Value = DBNull.Value;
                }
                retVal.Parameters.Add(parm);
            }
#if DEBUG
            this.m_tracer.TraceVerbose("Created SQL statement: {0}", retVal.CommandText);
            foreach (SqliteParameter v in retVal.Parameters)
                this.m_tracer.TraceVerbose(" --> [{0}] {1}", v.DbType, v.Value is byte[] ? BitConverter.ToString(v.Value as Byte[]).Replace("-", "") : v.Value);
#endif
            return retVal;
        }

        /// <summary>
        /// Map expando object
        /// </summary>
        private ExpandoObject MapExpando(IDataReader rdr)
        {
            try
            {
                var retVal = new ExpandoObject() as IDictionary<String, Object>;
                for (int i = 0; i < rdr.FieldCount; i++)
                {
                    var value = rdr[i];
                    var name = rdr.GetName(i);
                    if (value == DBNull.Value)
                        value = null;
                    if (value is byte[] && (value as byte[]).Length == 16)
                        value = new Guid(value as byte[]);
                    else if (
                        (name.ToLower().Contains("date") ||
                        name.ToLower().Contains("time") ||
                        name.ToLower().Contains("utc")) && (value is int || value is long))
                        value = new DateTime(value is int ? (int)value : (long)value, DateTimeKind.Utc).ToLocalTime();
                    retVal.Add(name, value);
                }
                return retVal as ExpandoObject;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error mapping data reader to expando object: {0}", e);
                throw;

            }
        }

        /// <summary>
        /// Executes the query
        /// </summary>
        public BisResultContext ExecuteQuery(BiQueryDefinition queryDefinition, IDictionary<string, object> parameters, BiAggregationDefinition[] aggregation, int offset, int? count)
        {
            if (queryDefinition == null)
                throw new ArgumentNullException(nameof(queryDefinition));

            // First we want to grab the connection strings used by this object
            var filledQuery = BiUtils.ResolveRefs(queryDefinition);

            // The ADO.NET provider only allows one connection to one db at a time, so verify the connections are appropriate
            if (queryDefinition.DataSources?.Count != 1)
                throw new InvalidOperationException($"ADO.NET BI queries can only source data from 1 connection source, query {queryDefinition.Name} has {queryDefinition.DataSources?.Count}");

            // Ensure we have sufficient priviledge
            foreach (var pol in queryDefinition.DataSources.SelectMany(o => o?.MetaData.Demands).Union(queryDefinition.MetaData?.Demands))
                ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(pol);

            // Apply defaults where possible
            foreach (var defaultParm in queryDefinition.Parameters.Where(p => !String.IsNullOrEmpty(p.DefaultValue) && !parameters.ContainsKey(p.Name)))
                parameters.Add(defaultParm.Name, defaultParm.DefaultValue);

            // Next we validate parameters
            if (!queryDefinition.Parameters.Where(p => p.Required == true).All(p => parameters.ContainsKey(p.Name)))
                throw new InvalidOperationException("Missing required parameter");

            // Validate parameter values
            foreach (var kv in parameters.ToArray())
            {
                var parmDef = queryDefinition.Parameters.FirstOrDefault(p => p.Name == kv.Key);
                if (parmDef == null) continue; // skip
                else switch (parmDef.Type)
                    {
                        case BisParameterDataType.Boolean:
                            parameters[kv.Key] = Boolean.Parse(kv.Value.ToString());
                            break;
                        case BisParameterDataType.Date:
                        case BisParameterDataType.DateTime:
                            parameters[kv.Key] = DateTime.Parse(kv.Value.ToString());
                            break;
                        case BisParameterDataType.Integer:
                            parameters[kv.Key] = Int32.Parse(kv.Value.ToString());
                            break;
                        case BisParameterDataType.String:
                            parameters[kv.Key] = kv.Value.ToString();
                            break;
                        case BisParameterDataType.Uuid:
                            parameters[kv.Key] = Guid.Parse(kv.Value.ToString());
                            break;
                        default:
                            throw new InvalidOperationException($"Cannot determine how to parse {parmDef.Type}");
                    }
            }

            // We want to open the specified connection
            var provider = "sqlite";
            var connectionString = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<DataConfigurationSection>().ConnectionString.FirstOrDefault(o => o.Name == queryDefinition.DataSources.First().ConnectionString);

            // Query definition
            var rdbmsQueryDefinition = queryDefinition.QueryDefinitions.FirstOrDefault(o => o.Invariants.Contains(provider));
            if (rdbmsQueryDefinition == null)
                throw new InvalidOperationException($"Could not find a query definition for invariant {provider}");

            // Prepare the templated SQL
            var parmRegex = new Regex(@"\$\{([\w_][\-\d\w\._]*?)\}");
            List<Object> values = new List<object>();
            var stmt = parmRegex.Replace(rdbmsQueryDefinition.Sql, (m) =>
            {
                object pValue = null;
                parameters.TryGetValue(m.Groups[1].Value, out pValue);
                values.Add(pValue);
                return "?";
            });

            // Aggregation definitions
            if (aggregation?.Length > 0)
            {
                var agg = aggregation.FirstOrDefault(o => o.Invariants?.Contains(provider) == true) ??
                    aggregation.FirstOrDefault(o => o.Invariants?.Count == 0) ??
                    aggregation.FirstOrDefault(o => o.Invariants == null);

                // Aggregation found
                if (agg == null)
                    throw new InvalidOperationException($"No provided aggregation can be found for {provider}");

                var selector = agg.Columns?.Select(c => {
                    switch (c.Aggregation)
                    {
                        case BiAggregateFunction.Average:
                            return $"AVG({c.ColumnSelector}) AS {c.Name}";
                        case BiAggregateFunction.Count:
                            return $"COUNT({c.ColumnSelector}) AS {c.Name}";
                        case BiAggregateFunction.CountDistinct:
                            return $"COUNT(DISTINCT {c.ColumnSelector}) AS {c.Name}";
                        case BiAggregateFunction.First:
                            return $"FIRST({c.ColumnSelector}) AS {c.Name}";
                        case BiAggregateFunction.Last:
                            return $"LAST({c.ColumnSelector}) AS {c.Name}";
                        case BiAggregateFunction.Max:
                            return $"MAX({c.ColumnSelector}) AS {c.Name}";
                        case BiAggregateFunction.Min:
                            return $"MIN({c.ColumnSelector}) AS {c.Name}";
                        case BiAggregateFunction.Sum:
                            return $"SUM({c.ColumnSelector}) AS {c.Name}";
                        case BiAggregateFunction.Value:
                            return $"{c.ColumnSelector} AS {c.Name}";
                        default:
                            throw new InvalidOperationException("Cannot apply aggregation function");
                    }
                }).ToArray() ?? new string[] { "*" };
                String[] groupings = agg.Groupings.Select(g => g.ColumnSelector).ToArray(),
                    colGroupings = agg.Groupings.Select(g => $"{g.ColumnSelector} AS {g.Name}").ToArray();
                // Aggregate
                stmt = $"SELECT {String.Join(",", colGroupings.Union(selector))} " +
                    $"FROM ({stmt}) AS _inner " +
                    $"GROUP BY {String.Join(",", groupings)}";
            }

            // Get a readonly connection
            var connParts = new SqliteConnectionStringBuilder(connectionString.Value);
            var file = connParts["dbfile"];
            var enc = connParts["encrypt"];
            using (SQLiteConnectionManager.Current.ExternLock(connectionString.Name))
                using (var conn = new SqliteConnection($"Data Source=\"{file}\""))
                {
                    try
                    {
                        // Decrypt database 
                        var securityKey = ApplicationContext.Current.GetCurrentContextSecurityKey();
                        if (securityKey != null && (enc ?? "true").Equals("true"))
                            conn.SetPassword(Encoding.UTF8.GetString(securityKey, 0, securityKey.Length));

                        // Open the database
                        conn.Open();

                        // Attach any other connection sources
                        foreach (var itm in queryDefinition.DataSources.Skip(1))
                        {
                            using (var attcmd = conn.CreateCommand())
                            {

                                var cstr = ApplicationContext.Current.ConfigurationManager.GetConnectionString(itm.ConnectionString);
                                if (cstr.GetComponent("encrypt") == "true")
                                    attcmd.CommandText = $"ATTACH DATABASE '{cstr.GetComponent("dbfile")}' AS {itm.Identifier} KEY ''";
                                else
                                    attcmd.CommandText = $"ATTACH DATABASE '{cstr.GetComponent("dbfile")}' AS {itm.Identifier} KEY X'{BitConverter.ToString(ApplicationContext.Current.GetCurrentContextSecurityKey()).Replace("-", "")}'";

                                attcmd.CommandType = System.Data.CommandType.Text;
                                attcmd.ExecuteNonQuery();
                            }
                        }

                        // Start time
                        DateTime startTime = DateTime.Now;
                        var sqlStmt = new SqlStatement(stmt, values.ToArray()).Limit(count ?? 10000).Offset(offset).Build();
                        this.m_tracer.TraceInfo("Executing BI Query: {0}", sqlStmt.Build().SQL);

                        // Create command for execution
                        using (var cmd = this.CreateCommand(conn, sqlStmt.SQL, sqlStmt.Arguments.ToArray()))
                        {
                            var results = new List<ExpandoObject>();
                            using (var rdr = cmd.ExecuteReader())
                                while (rdr.Read())
                                    results.Add(this.MapExpando(rdr));
                            return new BisResultContext(
                                queryDefinition,
                                parameters,
                                this,
                                results,
                                startTime);
                        }
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Error executing BIS data query: {0}", e);
                        throw new DataPersistenceException($"Error executing BIS data query", e);
                    }
                }
            
        }

        /// <summary>
        /// Execute the specified query
        /// </summary>
        public BisResultContext ExecuteQuery(string queryId, IDictionary<string, object> parameters, BiAggregationDefinition[] aggregation, int offset, int? count)
        {
            var query = ApplicationServiceContext.Current.GetService<IBiMetadataRepository>()?.Get<BiQueryDefinition>(queryId);
            if (query == null)
                throw new KeyNotFoundException(queryId);
            else
                return this.ExecuteQuery(query, parameters, aggregation, offset, count);
        }


        /// <summary>
        /// Executes the specified view
        /// </summary>
        public BisResultContext ExecuteView(BiViewDefinition viewDef, IDictionary<string, object> parameters, int offset, int? count)
        {
            viewDef = BiUtils.ResolveRefs(viewDef) as BiViewDefinition;
            var retVal = this.ExecuteQuery(viewDef.Query, parameters, viewDef.AggregationDefinitions?.ToArray(), offset, count);
            if (viewDef.Pivot != null)
                retVal = ApplicationServiceContext.Current.GetService<IBiPivotProvider>().Pivot(retVal, viewDef.Pivot);
            return retVal;
        }


    }
}
