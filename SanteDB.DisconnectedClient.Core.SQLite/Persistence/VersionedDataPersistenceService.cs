/*
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
using SanteDB.Core.Model;
using SanteDB.DisconnectedClient.SQLite.Model;
using System;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    public abstract class VersionedDataPersistenceService<TModel, TDomain> : VersionedDataPersistenceService<TModel, TDomain, TDomain>
    where TDomain : DbVersionedData, new()
    where TModel : VersionedEntityData<TModel>, new()
    {
    }

    /// <summary>
    /// Versioned domain data
    /// </summary>
    public abstract class VersionedDataPersistenceService<TModel, TDomain, TQueryResult> : BaseDataPersistenceService<TModel, TDomain, TQueryResult>
    where TDomain : DbVersionedData, new()
    where TModel : VersionedEntityData<TModel>, new()
    where TQueryResult : DbIdentified
    {

        /// <summary>
        /// Insert the data
        /// </summary>
        protected override TModel InsertInternal(SQLiteDataContext context, TModel data)
        {
            if (data.VersionKey.GetValueOrDefault() == Guid.Empty)
                data.VersionKey = Guid.NewGuid();
            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Update the data with new version information
        /// </summary>
        protected override TModel UpdateInternal(SQLiteDataContext context, TModel data)
        {
            var key = data.Key?.ToByteArray();
            if (!data.VersionKey.HasValue)
                data.VersionKey = Guid.NewGuid();
            else if (context.Connection.Table<TDomain>().Where(o => o.Uuid == key).ToList().FirstOrDefault()?.VersionKey == data.VersionKey)
                data.VersionKey = Guid.NewGuid();
            data.VersionSequence = null;
            return base.UpdateInternal(context, data);
        }

        /// <summary>
        /// Obsolete the specified data
        /// </summary>
        protected override TModel ObsoleteInternal(SQLiteDataContext context, TModel data)
        {
            data.PreviousVersionKey = data.VersionKey;
            data.VersionKey = Guid.NewGuid();
            data.VersionSequence = null;
            return base.ObsoleteInternal(context, data);
        }
    }
}
