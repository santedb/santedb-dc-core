/*
 * Based on OpenIZ, Copyright (C) 2015 - 2020 Mohawk College of Applied Arts and Technology
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
 * Date: 2019-11-27
 */
using SanteDB.Core.Data.QueryBuilder;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using SanteDB.DisconnectedClient.SQLite.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using SanteDB.Core.Model.Interfaces;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Base data persistence service
    /// </summary>
    public abstract class BaseDataPersistenceService<TModel, TDomain> : BaseDataPersistenceService<TModel, TDomain, TDomain>
        where TModel : BaseEntityData, new()
        where TDomain : DbBaseData, new()
    { }

    /// <summary>
    /// Base data persistence service
    /// </summary>
    public abstract class BaseDataPersistenceService<TModel, TDomain, TQueryResult> : IdentifiedPersistenceService<TModel, TDomain, TQueryResult>
        where TModel : BaseEntityData, new()
        where TDomain : DbBaseData, new()
        where TQueryResult : DbIdentified
    {

        /// <summary>
        /// Append order by statement
        /// </summary>
        protected override SqlStatement AppendOrderByStatement(SqlStatement domainQuery, ModelSort<TModel>[] orderBy)
        {
            domainQuery = base.AppendOrderByStatement(domainQuery, orderBy);
            return domainQuery.OrderBy<TDomain>(o => o.CreationTime, SanteDB.Core.Model.Map.SortOrderType.OrderByDescending);
        }

        /// <summary>
        /// Perform the actual insert.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        protected override TModel InsertInternal(SQLiteDataContext context, TModel data)
        {
            var domainObject = this.FromModelInstance(data, context) as TDomain;

            if (data.Key == Guid.Empty || !data.Key.HasValue)
                data.Key = domainObject.Key = Guid.NewGuid();

            // Ensure created by exists
            if (data.CreatedBy != null) data.CreatedBy = data.CreatedBy?.EnsureExists(context);
            data.CreatedByKey = domainObject.CreatedByKey = domainObject.CreatedByKey == Guid.Empty ? base.CurrentUserUuid(context) : domainObject.CreatedByKey;
            domainObject.CreationTime = domainObject.CreationTime == DateTimeOffset.MinValue || domainObject.CreationTime == null || domainObject.CreationTime.Value.DateTime == DateTime.MinValue ? DateTimeOffset.Now : domainObject.CreationTime;
            data.CreationTime = (DateTimeOffset)domainObject.CreationTime;
            domainObject.UpdatedByKey = domainObject.CreatedByKey == Guid.Empty || domainObject.CreatedByKey == null ? base.CurrentUserUuid(context) : domainObject.CreatedByKey;
            domainObject.UpdatedTime = DateTime.Now;

            // Special case system hiding of record
            if (data is ITaggable taggable)
            {
                var hideTag = taggable.Tags.FirstOrDefault(o => o.TagKey == "$sys.hidden")?.Value;
                if ("true".Equals(hideTag, StringComparison.OrdinalIgnoreCase) && domainObject is IDbHideable hideable)
                    hideable.Hidden = true;
            }

            if (!context.Connection.Table<TDomain>().Where(o => o.Uuid == domainObject.Uuid).Any())
                context.Connection.Insert(domainObject);
            else
                context.Connection.Update(domainObject);

            context.AddTransactedItem(data);
            return data;
        }

        /// <summary>
        /// Perform the actual update.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        protected override TModel UpdateInternal(SQLiteDataContext context, TModel data)
        {
            var domainObject = this.FromModelInstance(data, context) as TDomain;
            var existing = context.Connection.Table<TDomain>().Where(o => o.Uuid == domainObject.Uuid).FirstOrDefault();
            if (existing == null)
                throw new KeyNotFoundException($"Cannot find existing copy of {data}");

            // Created by is the updated by
            existing.CopyObjectData(domainObject);
            domainObject = existing;
            domainObject.CreatedByUuid = existing.CreatedByUuid;
            if (data.CreatedBy != null) data.CreatedBy = data.CreatedBy?.EnsureExists(context);
            domainObject.UpdatedByKey = domainObject.CreatedByKey == Guid.Empty || domainObject.CreatedByKey == null ? base.CurrentUserUuid(context) : domainObject.CreatedByKey;
            domainObject.UpdatedTime = DateTime.Now;
            
            // Special case, undelete
            if(!data.ObsoletedByKey.HasValue && existing.ObsoletionTime.HasValue)
            {
                domainObject.ObsoletionTime = null;
                domainObject.ObsoletedByUuid = null;
                //var model = TableMapping.Get(domainObject.GetType());
                //context.Connection.Execute($"UPDATE {model.TableName} SET {model.GetColumn(nameof(DbBaseData.ObsoletionTime)).Name} = null, {model.GetColumn(nameof(DbBaseData.ObsoletedByUuid)).Name} = null WHERE {model.GetColumn(nameof(DbBaseData.Uuid)).Name} = X'{BitConverter.ToString(domainObject.Uuid).Replace("-", "")}'");
            }

            // Special case system hiding of record
            if(data is ITaggable taggable)
            {
                var hideTag = taggable.Tags.FirstOrDefault(o => o.TagKey == "$sys.hidden")?.Value;
                if ("true".Equals(hideTag, StringComparison.OrdinalIgnoreCase) && domainObject is IDbHideable hideable)
                    hideable.Hidden = true;
            }

            context.Connection.Update(domainObject);
            context.AddTransactedItem(data);

            return data;
        }

        /// <summary>
        /// Performs the actual obsoletion
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        protected override TModel ObsoleteInternal(SQLiteDataContext context, TModel data)
        {
            var domainObject = this.FromModelInstance(data, context) as TDomain;
            if (data.ObsoletedBy != null) data.ObsoletedBy = data.ObsoletedBy?.EnsureExists(context);
            data.ObsoletedByKey = domainObject.ObsoletedByKey = data.ObsoletedBy?.Key ?? base.CurrentUserUuid(context);
            domainObject.ObsoletionTime = domainObject.ObsoletionTime ?? DateTime.Now;
            data.ObsoletionTime = (DateTimeOffset)domainObject.ObsoletionTime;
            var model = TableMapping.Get(domainObject.GetType());

            context.Connection.Execute($"UPDATE {model.TableName} SET {model.GetColumn(nameof(DbBaseData.ObsoletionTime)).Name} = {DateTime.Now.Ticks}, {model.GetColumn(nameof(DbBaseData.ObsoletedByUuid)).Name} = X'{BitConverter.ToString(domainObject.ObsoletedByUuid).Replace("-", "")}' WHERE {model.GetColumn(nameof(DbBaseData.Uuid)).Name} = X'{BitConverter.ToString(domainObject.Uuid).Replace("-", "")}'");
            return data;
        }


    }
}

