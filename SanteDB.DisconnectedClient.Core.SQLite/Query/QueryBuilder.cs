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
using SanteDB.Core.Model;
using SanteDB.Core.Model.Attributes;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using SanteDB.DisconnectedClient.SQLite.Model;

namespace SanteDB.DisconnectedClient.SQLite.Query
{

    /// <summary>
    /// Query predicate part
    /// </summary>
    public enum QueryPredicatePart
    {
        Full = Path | Guard | Cast | SubPath,
        Path = 0x1,
        Guard = 0x2,
        Cast = 0x4,
        SubPath = 0x8,
        PropertyAndGuard = Path | Guard,
        PropertyAndCast = Path | Cast
    }

    /// <summary>
    /// Represents the query predicate
    /// </summary>
    public class QueryPredicate
    {
        // Regex to extract property, guards and cast
        public static readonly Regex ExtractionRegex = new Regex(@"^(\w*?)(\[(.*?)\])?(\@(\w*))?(\.(.*))?$");

        private const int PropertyRegexGroup = 1;
        private const int GuardRegexGroup = 3;
        private const int CastRegexGroup = 5;
        private const int SubPropertyRegexGroup = 7;

        /// <summary>
        /// Gets or sets the path
        /// </summary>
        public String Path { get; private set; }

        /// <summary>
        /// Sub-path
        /// </summary>
        public String SubPath { get; private set; }

        /// <summary>
        /// Cast instruction
        /// </summary>
        public String CastAs { get; private set; }

        /// <summary>
        /// Guard condition
        /// </summary>
        public String Guard { get; private set; }

        /// <summary>
        /// Parse a condition
        /// </summary>
        public static QueryPredicate Parse(String condition)
        {
            var matches = ExtractionRegex.Match(condition);
            if (!matches.Success) return null;

            return new QueryPredicate()
            {
                Path = matches.Groups[PropertyRegexGroup].Value,
                CastAs = matches.Groups[CastRegexGroup].Value,
                Guard = matches.Groups[GuardRegexGroup].Value,
                SubPath = matches.Groups[SubPropertyRegexGroup].Value
            };
        }

        /// <summary>
        /// Represent the predicate as a string
        /// </summary>
        public String ToString(QueryPredicatePart parts)
        {
            StringBuilder sb = new StringBuilder();

            if ((parts & QueryPredicatePart.Path) != 0)
                sb.Append(this.Path);
            if ((parts & QueryPredicatePart.Guard) != 0 && !String.IsNullOrEmpty(this.Guard))
                sb.AppendFormat("[{0}]", this.Guard);
            if ((parts & QueryPredicatePart.Cast) != 0 && !String.IsNullOrEmpty(this.CastAs))
                sb.AppendFormat("@{0}", this.CastAs);
            if ((parts & QueryPredicatePart.SubPath) != 0 && !String.IsNullOrEmpty(this.SubPath))
                sb.AppendFormat("{0}{1}", sb.Length > 0 ? "." : "", this.SubPath);

            return sb.ToString();
        }

    }

    /// <summary>
    /// Query builder for model objects
    /// </summary>
    /// <remarks>
    /// Because the ORM used in the ADO persistence layer is very very lightweight, this query builder exists to parse 
    /// LINQ or HTTP query parameters into complex queries which implement joins/CTE/etc. across tables. Stuff that the
    /// classes in the little data model can't possibly support via LINQ expression.
    /// 
    /// To use this, simply pass a model based LINQ expression to the CreateQuery method. Examples are in the test project. 
    /// 
    /// Some reasons to use this:
    ///     - The generated SQL will gather all table instances up the object hierarchy for you (one hit instead of multiple)
    ///     - The queries it writes use efficient CTE tables
    ///     - It can do intelligent join conditions
    ///     - It uses Model LINQ expressions directly to SQL without the need to translate from Model LINQ to Domain LINQ queries
    /// </remarks>
    /// <example lang="cs" name="LINQ Expression illustrating join across tables">
    /// <![CDATA[QueryBuilder.CreateQuery<Patient>(o => o.DeterminerConcept.Mnemonic == "Instance")]]>
    /// </example>
    /// <example lang="sql" name="Resulting SQL query">
    /// <![CDATA[
    /// WITH 
    ///     cte0 AS (
    ///         SELECT cd_tbl.cd_id 
    ///         FROM cd_vrsn_tbl AS cd_vrsn_tbl 
    ///             INNER JOIN cd_tbl AS cd_tbl ON (cd_tbl.cd_id = cd_vrsn_tbl.cd_id) 
    ///         WHERE (cd_vrsn_tbl.mnemonic = ? )
    ///     )
    /// SELECT * 
    /// FROM pat_tbl AS pat_tbl 
    ///     INNER JOIN psn_tbl AS psn_tbl ON (pat_tbl.ent_vrsn_id = psn_tbl.ent_vrsn_id) 
    ///     INNER JOIN ent_vrsn_tbl AS ent_vrsn_tbl ON (psn_tbl.ent_vrsn_id = ent_vrsn_tbl.ent_vrsn_id) 
    ///     INNER JOIN ent_tbl AS ent_tbl ON (ent_tbl.ent_id = ent_vrsn_tbl.ent_id) 
    ///     INNER JOIN cte0 ON (ent_tbl.dtr_cd_id = cte0.cd_id)
    /// ]]>
    /// </example>
    public class QueryBuilder
    {

        // Join cache
        private Dictionary<String, KeyValuePair<SqlStatement, List<TableMapping>>> s_joinCache = new Dictionary<String, KeyValuePair<SqlStatement, List<TableMapping>>>();

        // Filter function regex
        public static readonly Regex ExtendedFunctionRegex = new Regex(@"^:\((\w*?)(\|(.*?)\)|\))(.*)");

        // Filter functions
        private static Dictionary<String, IDbFilterFunction> s_filterFunctions = null;

        // Mapper
        private ModelMapper m_mapper;
        private const int PropertyRegexGroup = 1;
        private const int GuardRegexGroup = 3;
        private const int CastRegexGroup = 5;
        private const int SubPropertyRegexGroup = 7;

        // A list of hacks injected into this query builder
        private List<IQueryBuilderHack> m_hacks = new List<IQueryBuilderHack>();

        /// <summary>
        /// Represents model mapper
        /// </summary>
        /// <param name="mapper"></param>
        public QueryBuilder(ModelMapper mapper, params IQueryBuilderHack[] hacks)
        {
            this.m_mapper = mapper;
            this.m_hacks = hacks.ToList();
        }

        /// <summary>
        /// Create a query 
        /// </summary>
        public SqlStatement CreateQuery<TModel>(Expression<Func<TModel, bool>> predicate, ModelSort<TModel>[] orderBy)
        {
            var nvc = QueryExpressionBuilder.BuildQuery(predicate, true);
            return CreateQuery<TModel>(nvc, orderBy);
        }

        /// <summary>
        /// Create query
        /// </summary>
        public SqlStatement CreateQuery<TModel>(IEnumerable<KeyValuePair<String, Object>> query, ModelSort<TModel>[] orderBy, params ColumnMapping[] selector)
        {
            return CreateQuery<TModel>(query, null, orderBy, selector);
        }

        /// <summary>
        /// Create query 
        /// </summary>
        public SqlStatement CreateQuery<TModel>(IEnumerable<KeyValuePair<String, Object>> query, String tablePrefix, ModelSort<TModel>[] orderBy, params ColumnMapping[] selector)
        {
            return CreateQuery<TModel>(query, null, false, orderBy, selector);
        }

        /// <summary>
        /// Query query 
        /// </summary>
        /// <param name="query"></param>
        public SqlStatement CreateQuery<TModel>(IEnumerable<KeyValuePair<String, Object>> query, String tablePrefix, bool skipJoins, ModelSort<TModel>[] orderBy, params ColumnMapping[] selector)
        {
            var tableType = m_mapper.MapModelType(typeof(TModel));
            var tableMap = TableMapping.Get(tableType);
            List<TableMapping> scopedTables = new List<TableMapping>() { tableMap };

            bool skipParentJoin = true;
            SqlStatement selectStatement = null;
            if (skipJoins)
            {
                selectStatement = new SqlStatement($" FROM {tableMap.TableName} AS {tablePrefix}{tableMap.TableName} ");
            }
            else
            {
                selectStatement = new SqlStatement($" FROM {tableMap.TableName} AS {tablePrefix}{tableMap.TableName} ");

                Stack<TableMapping> fkStack = new Stack<TableMapping>();
                fkStack.Push(tableMap);
                // Always join tables?
                do
                {
                    var dt = fkStack.Pop();
                    foreach (var jt in dt.Columns.Where(o => o.IsAlwaysJoin))
                    {
                        var fkTbl = TableMapping.Get(jt.ForeignKey.Table);
                        var fkAtt = fkTbl.GetColumn(jt.ForeignKey.Column);

                        if (typeof(IDbHideable).IsAssignableFrom(fkTbl.OrmType))
                            selectStatement.Append($"INNER JOIN {fkAtt.Table.TableName} AS {tablePrefix}{fkAtt.Table.TableName} ON ({tablePrefix}{jt.Table.TableName}.{jt.Name} = {tablePrefix}{fkAtt.Table.TableName}.{fkAtt.Name} AND {tablePrefix}{fkAtt.Table.TableName}.hidden = 0) ");
                        else
                            selectStatement.Append($"INNER JOIN {fkAtt.Table.TableName} AS {tablePrefix}{fkAtt.Table.TableName} ON ({tablePrefix}{jt.Table.TableName}.{jt.Name} = {tablePrefix}{fkAtt.Table.TableName}.{fkAtt.Name}) ");
                        if (!scopedTables.Contains(fkTbl))
                            fkStack.Push(fkTbl);
                        scopedTables.Add(fkAtt.Table);
                    }
                } while (fkStack.Count > 0);

                // Add the heavy work to the cache
                lock (s_joinCache)
                    if (!s_joinCache.ContainsKey($"{tablePrefix}.{typeof(TModel).Name}"))
                        s_joinCache.Add($"{tablePrefix}.{typeof(TModel).Name}", new KeyValuePair<SqlStatement, List<TableMapping>>(selectStatement.Build(), scopedTables));

            }

            // Column definitions
            var columnSelector = selector;
            if (selector == null || selector.Length == 0)
                columnSelector = scopedTables.SelectMany(o => o.Columns).ToArray();
            // columnSelector = scopedTables.SelectMany(o => o.Columns).ToArray();

            List<String> flatNames = new List<string>();
            var columnList = String.Join(",", columnSelector.Select(o =>
                {
                    var rootCol = tableMap.GetColumn(o.SourceProperty);
                    skipParentJoin &= rootCol != null;
                    if (!flatNames.Contains(o.Name))
                    {
                        flatNames.Add(o.Name);
                        return $"{tablePrefix}{o.Table.TableName}.{o.Name} AS \"{o.Name}\"";
                    }
                    else if (skipParentJoin)
                        return $"{tablePrefix}{rootCol.Table.TableName}.{rootCol.Name}";
                    else
                        return $"{tablePrefix}{o.Table.TableName}.{o.Name}";
                }));
            selectStatement = new SqlStatement($"SELECT {columnList} ").Append(selectStatement);


            // We want to process each query and build WHERE clauses - these where clauses are based off of the JSON / XML names
            // on the model, so we have to use those for the time being before translating to SQL
            List<KeyValuePair<String, Object>> workingParameters = new List<KeyValuePair<string, object>>(query);

            // Where clause
            SqlStatement whereClause = new SqlStatement();
            List<SqlStatement> cteStatements = new List<SqlStatement>();

            // Construct
            while (workingParameters.Count > 0)
            {
                var parm = workingParameters.First();
                workingParameters.RemoveAt(0);

                // Match the regex and process
                var propertyPredicate = QueryPredicate.Parse(parm.Key);
                if (propertyPredicate == null) throw new ArgumentOutOfRangeException(parm.Key);

                // Next, we want to construct the 
                var otherParms = workingParameters.Where(o => QueryPredicate.Parse(o.Key).ToString(QueryPredicatePart.PropertyAndCast) == propertyPredicate.ToString(QueryPredicatePart.PropertyAndCast)).ToArray();

                // Remove the working parameters if the column is FK then all parameters
                if (otherParms.Any() || !String.IsNullOrEmpty(propertyPredicate.Guard) || !String.IsNullOrEmpty(propertyPredicate.SubPath))
                {
                    foreach (var o in otherParms)
                        workingParameters.Remove(o);

                    // We need to do a sub query

                    IEnumerable<KeyValuePair<String, Object>> queryParms = new List<KeyValuePair<String, Object>>() { parm }.Union(otherParms);

                    // Grab the appropriate builder
                    var subProp = typeof(TModel).GetQueryProperty(propertyPredicate.Path, true);
                    if (subProp == null) throw new MissingMemberException(propertyPredicate.Path);

                    // Link to this table in the other?
                    // Is this a collection?
                    if (!this.m_hacks.Any(o => o.HackQuery(this, selectStatement, whereClause, typeof(TModel), subProp, tablePrefix, propertyPredicate, parm.Value, scopedTables)))
                    {
                        if (typeof(IList).GetTypeInfo().IsAssignableFrom(subProp.PropertyType.GetTypeInfo())) // Other table points at this on
                        {
                            var propertyType = subProp.PropertyType.StripGeneric();
                            // map and get ORM def'n
                            var subTableType = m_mapper.MapModelType(propertyType);
                            var subTableMap = TableMapping.Get(subTableType);
                            var linkColumns = subTableMap.Columns.Where(o => scopedTables.Any(s => s.OrmType == o.ForeignKey?.Table));
                            var linkColumn = linkColumns.Count() > 1 ? linkColumns.FirstOrDefault(o => propertyPredicate.SubPath.StartsWith("source") ? o.SourceProperty.Name != "SourceUuid" : o.SourceProperty.Name == "SourceUuid") : linkColumns.FirstOrDefault();
                            // Link column is null, is there an assoc attrib?
                            SqlStatement subQueryStatement = new SqlStatement();

                            var subTableColumn = linkColumn;
                            string existsClause = String.Empty;

                            if (linkColumn == null)
                            {
                                var tableWithJoin = scopedTables.Select(o => o.AssociationWith(subTableMap)).FirstOrDefault();
                                linkColumn = tableWithJoin.Columns.SingleOrDefault(o => scopedTables.Any(s => s.OrmType == o.ForeignKey?.Table));
                                var targetColumn = tableWithJoin.Columns.SingleOrDefault(o => o.ForeignKey?.Table == subTableMap.OrmType);
                                subTableColumn = subTableMap.GetColumn(targetColumn.ForeignKey.Column);
                                // The sub-query statement needs to be joined as well 
                                var lnkPfx = IncrementSubQueryAlias(tablePrefix);
                                subQueryStatement.Append($"SELECT {lnkPfx}{tableWithJoin.TableName}.{linkColumn.Name} FROM {tableWithJoin.TableName} AS {lnkPfx}{tableWithJoin.TableName} WHERE ");
                                existsClause = $"{lnkPfx}{tableWithJoin.TableName}.{targetColumn.Name}";
                                //throw new InvalidOperationException($"Cannot find foreign key reference to table {tableMap.TableName} in {subTableMap.TableName}");
                            }

                            // Local Table
                            var localTable = scopedTables.Where(o => o.GetColumn(linkColumn.ForeignKey.Column) != null).FirstOrDefault();
                            if (String.IsNullOrEmpty(existsClause))
                                existsClause = $"{tablePrefix}{localTable.TableName}.{localTable.GetColumn(linkColumn.ForeignKey.Column).Name}";

                            // Guards
                            var guardConditions = queryParms.GroupBy(o => QueryPredicate.Parse(o.Key).Guard);
                            int nGuards = 0;
                            foreach (var guardClause in guardConditions)
                            {

                                var subQuery = guardClause.Select(o => new KeyValuePair<String, Object>(QueryPredicate.Parse(o.Key).ToString(QueryPredicatePart.SubPath), o.Value)).ToList();
                                string[] guardValues = guardClause.Key.Split('|');

                                // TODO: GUARD CONDITION HERE!!!!
                                if (!String.IsNullOrEmpty(guardClause.Key))
                                {
                                    StringBuilder guardCondition = new StringBuilder();
                                    var clsModel = propertyType;
                                    while (clsModel.GetTypeInfo().GetCustomAttribute<ClassifierAttribute>() != null)
                                    {
                                        var clsProperty = clsModel.GetRuntimeProperty(clsModel.GetTypeInfo().GetCustomAttribute<ClassifierAttribute>().ClassifierProperty);
                                        clsModel = clsProperty.PropertyType.StripGeneric();
                                        var redirectProperty = clsProperty.GetCustomAttribute<SerializationReferenceAttribute>()?.RedirectProperty;
                                        if (redirectProperty != null)
                                            clsProperty = clsProperty.DeclaringType.GetRuntimeProperty(redirectProperty);

                                        guardCondition.Append(clsProperty.GetSerializationName());
                                        if (typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(clsModel.GetTypeInfo()))
                                            guardCondition.Append(".");

                                        if (clsProperty.PropertyType.GetTypeInfo().IsEnum)
                                            guardValues = guardValues.Select(o => ((int)Enum.Parse(clsProperty.PropertyType, o)).ToString()).ToArray();
                                    }
                                    subQuery.Add(new KeyValuePair<string, object>(guardCondition.ToString(), guardValues));
                                }

                                // Generate method
                                var prefix = IncrementSubQueryAlias(tablePrefix);
                                var genMethod = typeof(QueryBuilder).GetGenericMethod("CreateQuery", new Type[] { propertyType }, new Type[] { subQuery.GetType(), typeof(String), typeof(bool), typeof(ModelSort<>).MakeGenericType(propertyType).MakeArrayType(), typeof(ColumnMapping[]) });

                                // Sub path is specified
                                if (String.IsNullOrEmpty(propertyPredicate.SubPath) && "null".Equals(parm.Value))
                                    subQueryStatement.And($" {existsClause} NOT IN (");
                                else
                                    subQueryStatement.And($" {existsClause} IN (");

                                nGuards++;
                                existsClause = $"{prefix}{subTableColumn.Table.TableName}.{subTableColumn.Name}";

                                if (subQuery.Count(p => !p.Key.Contains(".")) == 0)
                                    subQueryStatement.Append(genMethod.Invoke(this, new Object[] { subQuery, prefix, true, null, new ColumnMapping[] { subTableColumn } }) as SqlStatement);
                                else
                                    subQueryStatement.Append(genMethod.Invoke(this, new Object[] { subQuery, prefix, false, null, new ColumnMapping[] { subTableColumn } }) as SqlStatement);


                                //// TODO: Check if limiting the the query is better
                                //if (guardConditions.Last().Key != guardClause.Key)
                                //    subQueryStatement.Append(" INTERSECT ");
                            }

                            // Unwind guards
                            while (nGuards-- > 0)
                                subQueryStatement.Append(")");

                            if (subTableColumn != linkColumn)
                                whereClause.And($"{tablePrefix}{localTable.TableName}.{localTable.GetColumn(linkColumn.ForeignKey.Column).Name} IN (").Append(subQueryStatement).Append(")");
                            else
                                whereClause.And(subQueryStatement);
                        }
                        else // this table points at other
                        {
                            var subQuery = queryParms.Select(o => new KeyValuePair<String, Object>(QueryPredicate.Parse(o.Key).ToString(QueryPredicatePart.SubPath), o.Value)).ToList();
                            TableMapping tableMapping = null;
                            var subPropKey = typeof(TModel).GetQueryProperty(propertyPredicate.Path);

                            // Get column info
                            PropertyInfo domainProperty = scopedTables.Select(o => { tableMapping = o; return m_mapper.MapModelProperty(typeof(TModel), o.OrmType, subPropKey); })?.FirstOrDefault(o => o != null);
                            ColumnMapping linkColumn = null;
                            // If the domain property is not set, we may have to infer the link
                            if (domainProperty == null)
                            {
                                var subPropType = m_mapper.MapModelType(subProp.PropertyType);
                                // We find the first column with a foreign key that points to the other !!!
                                linkColumn = scopedTables.SelectMany(o => o.Columns).FirstOrDefault(o => o.ForeignKey?.Table == subPropType);
                            }
                            else
                                linkColumn = tableMapping.GetColumn(domainProperty);

                            var fkTableDef = TableMapping.Get(linkColumn.ForeignKey.Table);
                            var fkColumnDef = fkTableDef.GetColumn(linkColumn.ForeignKey.Column);

                            // Create the sub-query
                            SqlStatement subQueryStatement = null;
                            var subSkipJoins = subQuery.Count(o => !o.Key.Contains(".") && o.Key != "obsoletionTime") == 0;

                            if (String.IsNullOrEmpty(propertyPredicate.CastAs))
                            {
                                var genMethod = typeof(QueryBuilder).GetGenericMethod("CreateQuery", new Type[] { subProp.PropertyType }, new Type[] { subQuery.GetType(), typeof(string), typeof(bool), typeof(ModelSort<>).MakeGenericType(subProp.PropertyType).MakeArrayType(), typeof(ColumnMapping[]) });
                                subQueryStatement = genMethod.Invoke(this, new Object[] { subQuery, null, subSkipJoins, null, new ColumnMapping[] { fkColumnDef } }) as SqlStatement;
                            }
                            else // we need to cast!
                            {
                                var castAsType = new SanteDB.Core.Model.Serialization.ModelSerializationBinder().BindToType("SanteDB.Core.Model", propertyPredicate.CastAs);

                                var genMethod = typeof(QueryBuilder).GetGenericMethod("CreateQuery", new Type[] { castAsType }, new Type[] { subQuery.GetType(), typeof(String), typeof(bool), typeof(ModelSort<>).MakeGenericType(castAsType).MakeArrayType(), typeof(ColumnMapping[]) });
                                subQueryStatement = genMethod.Invoke(this, new Object[] { subQuery, null, false, null, new ColumnMapping[] { fkColumnDef } }) as SqlStatement;
                            }

                            cteStatements.Add(new SqlStatement($"{tablePrefix}cte{cteStatements.Count} AS (").Append(subQueryStatement).Append(")"));

                            //subQueryStatement.And($"{tablePrefix}{tableMapping.TableName}.{linkColumn.Name} = {sqName}{fkTableDef.TableName}.{fkColumnDef.Name} ");

                            //selectStatement.Append($"INNER JOIN {tablePrefix}cte{cteStatements.Count - 1} ON ({tablePrefix}{tableMapping.TableName}.{linkColumn.Name} = {tablePrefix}cte{cteStatements.Count - 1}.{fkColumnDef.Name})");
                            whereClause.And($"{tablePrefix}{tableMapping.TableName}.{linkColumn.Name} IN (SELECT {tablePrefix}cte{cteStatements.Count - 1}.{fkColumnDef.Name} FROM {tablePrefix}cte{cteStatements.Count - 1})");

                        }

                    }
                }
                else if (!this.m_hacks.Any(o => o.HackQuery(this, selectStatement, whereClause, typeof(TModel), typeof(TModel).GetQueryProperty(propertyPredicate.Path), tablePrefix, propertyPredicate, parm.Value, scopedTables)))
                    whereClause.And(CreateWhereCondition(typeof(TModel), propertyPredicate.Path, parm.Value, tablePrefix, scopedTables));

            }

            // Return statement
            SqlStatement retVal = new SqlStatement();
            if (cteStatements.Count > 0)
            {
                retVal.Append("WITH ");
                foreach (var c in cteStatements)
                {
                    retVal.Append(c);
                    if (c != cteStatements.Last())
                        retVal.Append(",");
                }
            }
            retVal.Append(selectStatement.Where(whereClause));

            // Is the type hideable
            if (typeof(IDbHideable).IsAssignableFrom(tableType))
                retVal.And(" hidden = 0");

            // TODO: Order by?
            if (orderBy != null && orderBy.Length > 0)
            {
                retVal.Append(" ORDER BY");
                foreach (var ob in orderBy)
                {
                    // Query property path 
                    var orderStatement = this.CreateOrderBy(typeof(TModel), tablePrefix, scopedTables, ob.SortProperty.Body, ob.SortOrder);
                    retVal.Append(orderStatement).Append(",");
                }
                retVal.RemoveLast();
            }
            return retVal;
        }

        /// <summary>
        /// Create the order by clause
        /// </summary>
        /// <param name="tmodel">The type of model</param>
        /// <param name="tablePrefix">The prefix that the table has</param>
        /// <param name="scopedTables">The tables which are scoped</param>
        /// <param name="sortExpression">The sorting expression</param>
        private SqlStatement CreateOrderBy(Type tmodel, string tablePrefix, List<TableMapping> scopedTables, Expression sortExpression, SortOrderType order)
        {
            switch (sortExpression.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    return this.CreateOrderBy(tmodel, tablePrefix, scopedTables, ((UnaryExpression)sortExpression).Operand, order);
                case ExpressionType.MemberAccess:
                    var mexpr = (MemberExpression)sortExpression;

                    // Determine the parameter type 
                    if (mexpr.Expression.NodeType != ExpressionType.Parameter)
                        throw new InvalidOperationException("OrderBy can only be performed on primary properties of the object");

                    // Determine the map
                    var tableMapping = scopedTables.First();
                    var propertyInfo = mexpr.Member as PropertyInfo;
                    PropertyInfo domainProperty = scopedTables.Select(o => { tableMapping = o; return m_mapper.MapModelProperty(tmodel, o.OrmType, propertyInfo); }).FirstOrDefault(o => o != null);
                    var columnData = tableMapping.GetColumn(domainProperty);
                    return new SqlStatement($" {columnData.Name} {(order == SortOrderType.OrderBy ? "ASC" : "DESC")}");

                default:
                    throw new InvalidOperationException("Cannot sort by this property expression");
            }
        }

        /// <summary>
        /// Increment sub-query alias
        /// </summary>
        private static String IncrementSubQueryAlias(string tablePrefix)
        {
            if (String.IsNullOrEmpty(tablePrefix))
                return "sq0";
            else
            {
                int sq = 0;
                if (Int32.TryParse(tablePrefix.Substring(2), out sq))
                    return "sq" + (sq + 1);
                else
                    return "sq0";
            }
        }

        /// <summary>
        /// Gets the filter function
        /// </summary>
        public IDbFilterFunction GetFilterFunction(string name)
        {
            if (s_filterFunctions == null)
            {
                s_filterFunctions = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.IsDynamic)
                        .SelectMany(a =>
                        {
                            try
                            {
                                return a.ExportedTypes;
                            }
                            catch { return Type.EmptyTypes; }
                        })
                        .Where(t => typeof(IDbFilterFunction).IsAssignableFrom(t) && !t.IsAbstract)
                        .Select(t => Activator.CreateInstance(t) as IDbFilterFunction)
                        .ToDictionary(o => o.Name, o => o);
            }
            IDbFilterFunction retVal = null;
            s_filterFunctions.TryGetValue(name, out retVal);
            return retVal;
        }


        /// <summary>
        /// Create a single where condition based on the property info
        /// </summary>
        public SqlStatement CreateWhereCondition(Type tmodel, String propertyPath, Object value, String tablePrefix, List<TableMapping> scopedTables, String tableAlias = null)
        {

            SqlStatement retVal = new SqlStatement();

            // Map the type
            var tableMapping = scopedTables.First();
            var propertyInfo = tmodel.GetQueryProperty(propertyPath);
            if (propertyInfo == null)
                throw new ArgumentOutOfRangeException(propertyPath);
            PropertyInfo domainProperty = scopedTables.Select(o => { tableMapping = o; return m_mapper.MapModelProperty(tmodel, o.OrmType, propertyInfo); }).FirstOrDefault(o => o != null);

            // Now map the property path
            if (String.IsNullOrEmpty(tableAlias))
                tableAlias = $"{tablePrefix}{tableMapping.TableName}";
            if (domainProperty == null)
            {
                return new SqlStatement("1");
                //throw new ArgumentException($"Can't find SQL based property for {propertyPath} on {tableMapping.TableName}");
            }
            var columnData = tableMapping.GetColumn(domainProperty);

            // List of parameters
            var lValue = value as IList;
            if (lValue == null)
                lValue = new List<Object>() { value };

            lValue = lValue.OfType<Object>().Distinct().ToList();

            retVal.Append("(");
            for (var i = 0; i < lValue.Count; i++)
            {
                var itm = lValue[i];
                retVal.Append($"{tableAlias}.{columnData.Name}");
                var semantic = " OR ";
                var iValue = itm;
                if (iValue is String)
                {
                    var sValue = itm as String;
                    switch (sValue[0])
                    {
                        case ':': // function
                            var opMatch = ExtendedFunctionRegex.Match(sValue);
                            List<String> extendedParms = new List<string>();

                            if (opMatch.Success)
                            {
                                // Extract
                                String fnName = opMatch.Groups[1].Value,
                                    parms = opMatch.Groups[3].Value,
                                    operand = opMatch.Groups[4].Value;

                                var parmExtract = QueryFilterExtensions.ParameterExtractRegex.Match(parms + ",");
                                while (parmExtract.Success)
                                {
                                    extendedParms.Add(parmExtract.Groups[1].Value);
                                    parmExtract = QueryFilterExtensions.ParameterExtractRegex.Match(parmExtract.Groups[2].Value);
                                }

                                // Now find the function
                                var filterFn = GetFilterFunction(fnName);
                                if (filterFn == null)
                                    throw new EntryPointNotFoundException($"No extended filter {fnName} found");
                                else
                                {
                                    retVal.RemoveLast();
                                    retVal = filterFn.CreateSqlStatement(retVal, $"{tableAlias}.{columnData.Name}", extendedParms.ToArray(), operand, domainProperty.PropertyType).Build();
                                }
                            }
                            else
                                retVal.Append($" = ? ", CreateParameterValue(sValue, domainProperty.PropertyType));
                            break;
                        case '<':
                            semantic = " AND ";
                            if (sValue[1] == '=')
                                retVal.Append(" <= ?", CreateParameterValue(sValue.Substring(2), propertyInfo.PropertyType));
                            else
                                retVal.Append(" < ?", CreateParameterValue(sValue.Substring(1), propertyInfo.PropertyType));
                            break;
                        case '>':
                            // peek the next value and see if it is < then we use BETWEEN
                            if (i < lValue.Count - 1 && lValue[i + 1].ToString().StartsWith("<"))
                            {
                                object lower = null, upper = null;
                                if (sValue[1] == '=')
                                    lower = CreateParameterValue(sValue.Substring(2), propertyInfo.PropertyType);
                                else
                                    lower = CreateParameterValue(sValue.Substring(1), propertyInfo.PropertyType);
                                sValue = lValue[++i].ToString();
                                if (sValue[1] == '=')
                                    upper = CreateParameterValue(sValue.Substring(2), propertyInfo.PropertyType);
                                else
                                    upper = CreateParameterValue(sValue.Substring(1), propertyInfo.PropertyType);
                                semantic = " OR ";
                                retVal.Append($" BETWEEN ? AND ?", lower, upper);
                            }
                            else
                            {
                                semantic = " AND ";
                                if (sValue[1] == '=')
                                    retVal.Append($" >= ?", CreateParameterValue(sValue.Substring(2), propertyInfo.PropertyType));
                                else
                                    retVal.Append($" > ?", CreateParameterValue(sValue.Substring(1), propertyInfo.PropertyType));
                            }
                            break;

                        case '!':
                            semantic = " AND ";
                            if (sValue.Equals("!null"))
                                retVal.Append(" IS NOT NULL");
                            else
                                retVal.Append(" <> ?", CreateParameterValue(sValue.Substring(1), propertyInfo.PropertyType));
                            break;
                        case '~':
                            if (sValue.Contains("*") || sValue.Contains("?"))
                                retVal.Append(" LIKE ? ", CreateParameterValue(sValue.Substring(1).Replace("*", "%"), propertyInfo.PropertyType));
                            else
                                retVal.Append(" LIKE '%' || ? || '%'", CreateParameterValue(sValue.Substring(1), propertyInfo.PropertyType));
                            break;
                        case '^':
                            retVal.Append(" LIKE ? || '%'", CreateParameterValue(sValue.Substring(1), propertyInfo.PropertyType));
                            break;
                        case '$':
                            retVal.Append(" LIKE '%' || ?", CreateParameterValue(sValue.Substring(1), propertyInfo.PropertyType));
                            break;
                        default:
                            if (sValue.Equals("null"))
                                retVal.Append(" IS NULL");
                            else
                                retVal.Append(" = ? ", CreateParameterValue(sValue, propertyInfo.PropertyType));
                            break;
                    }
                }
                else
                    retVal.Append(" = ? ", CreateParameterValue(iValue, propertyInfo.PropertyType));

                if (i < lValue.Count - 1)
                    retVal.Append(semantic);
            }

            retVal.Append(")");

            return retVal;
        }

        /// <summary>
        /// Create a parameter value
        /// </summary>
        internal static object CreateParameterValue(object value, Type toType)
        {
            object retVal = null;

            if (value is String str && str.StartsWith("\"") && str.EndsWith("\"")) // quoted string
                value = str.Substring(1, str.Length - 2).Replace("\\\"", "\"");

            if (value is Guid)
                retVal = ((Guid)value).ToByteArray();
            else if (value.GetType() == toType ||
                value.GetType() == toType.StripNullable())
                retVal = value;
            else if (!MapUtil.TryConvert(value, toType, out retVal))
                throw new ArgumentOutOfRangeException(value.ToString());

            // Dates in SQLite are UTC so lets convert
            if (retVal is DateTime)
                retVal = ((DateTime)retVal).ToUniversalTime();
            else if (retVal is DateTimeOffset)
                retVal = ((DateTimeOffset)retVal).ToUniversalTime();

            return retVal;
        }
    }
}
