﻿/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
using SanteDB.Core;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.SQLite.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.SQLite.Mail.Hacks
{
    /// <summary>
    /// Represents a query hack for participations / relationships where the guard is being queried 
    /// </summary>
    public class RecipientQueryHack : IQueryBuilderHack
    {

        public bool HackQuery(QueryBuilder builder, SqlStatement sqlStatement, SqlStatement whereClause, Type tmodel, PropertyInfo property, string queryPrefix, QueryPredicate predicate, object values, IEnumerable<TableMapping> scopedTables)
        {
            if (property.Name == "RcptTo" && property.PropertyType.StripGeneric() == typeof(SecurityUser))
            {
                if (predicate.SubPath == "userName")
                {
                    if (!(values is IList))
                        values = new List<Object>() { values };

                    var lValues = values as IList;
                    var secRepo = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>();
                    Guid[] vals = lValues.OfType<String>().Select(u => secRepo.GetUser(u)?.Key).OfType<Guid>().ToArray();
                    whereClause.And($"rcptTo IN ({String.Join(",", vals.Select(o => $"X'{BitConverter.ToString(((Guid)o).ToByteArray()).Replace("-", "")}'").ToArray())})");
                    return true;
                }
                else
                    throw new InvalidOperationException("Cannot map this expression");
            }
            return false;
        }

    }
}
