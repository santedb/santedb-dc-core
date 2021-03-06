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
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SanteDB.DisconnectedClient.SQLite.Query
{
    /// <summary>
    /// Query builder hack
    /// </summary>
    public interface IQueryBuilderHack
    {

        /// <summary>
        /// Hacks the query in some manner
        /// </summary>
        /// <param name="sqlStatement">The current vanilla (no WHERE clause) query</param>
        /// <param name="whereClause">The current where clause</param>
        /// <param name="property">The property which is currently being hacked</param>
        /// <param name="predicate">The current predicate</param>
        /// <param name="scopedTables">The tables that are scoped for the current query</param>
        /// <returns></returns>
        bool HackQuery(QueryBuilder builder, SqlStatement sqlStatement, SqlStatement whereClause, Type tmodel, PropertyInfo property, String queryPrefix, QueryPredicate predicate, Object values, IEnumerable<TableMapping> scopedTables);

    }
}
