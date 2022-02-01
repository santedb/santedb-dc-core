/*
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
using SanteDB.Core.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents the persistence service for application eneities
    /// </summary>
    public class ApplicationEntityPersistenceService : EntityDerivedPersistenceService<ApplicationEntity, DbApplicationEntity, DbApplicationEntity>
    {
        /// <summary>
        /// To model instance
        /// </summary>
        public override ApplicationEntity ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var applicationEntity = dataInstance as DbApplicationEntity;
            var dbe = context.Connection.Table<DbEntity>().Where(o => o.Uuid == applicationEntity.Uuid).First();
            var retVal = m_entityPersister.ToModelInstance<ApplicationEntity>(dbe, context);
            retVal.SecurityApplicationKey = new Guid(applicationEntity.SecurityApplicationUuid);
            retVal.SoftwareName = applicationEntity.SoftwareName;
            retVal.VersionName = applicationEntity.VersionName;
            retVal.VendorName = applicationEntity.VendorName;
            //retVal.LoadAssociations(context);
            return retVal;
        }

        /// <summary>
        /// Insert the application entity
        /// </summary>
        protected override ApplicationEntity InsertInternal(SQLiteDataContext context, ApplicationEntity data)
        {
            if (data.SecurityApplication != null) data.SecurityApplication = data.SecurityApplication?.EnsureExists(context);
            data.SecurityApplicationKey = data.SecurityApplication?.Key ?? data.SecurityApplicationKey;
            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Update the application entity
        /// </summary>
        protected override ApplicationEntity UpdateInternal(SQLiteDataContext context, ApplicationEntity data)
        {
            if (data.SecurityApplication != null) data.SecurityApplication = data.SecurityApplication?.EnsureExists(context);
            data.SecurityApplicationKey = data.SecurityApplication?.Key ?? data.SecurityApplicationKey;
            return base.UpdateInternal(context, data);
        }
    }
}
