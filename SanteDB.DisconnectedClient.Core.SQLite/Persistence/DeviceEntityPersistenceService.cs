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
using SanteDB.Core.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents a persistence service for a device entity
    /// </summary>
    public class DeviceEntityPersistenceService : EntityDerivedPersistenceService<DeviceEntity, DbDeviceEntity, DbDeviceEntity>
    {
        /// <summary>
        /// Convert the database representation to a model instance
        /// </summary>
        public override DeviceEntity ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var deviceEntity = dataInstance as DbDeviceEntity;
            var dbe = context.Connection.Table<DbEntity>().Where(o => o.Uuid == deviceEntity.Uuid).First();
            var retVal = m_entityPersister.ToModelInstance<DeviceEntity>(dbe, context);
            retVal.SecurityDeviceKey = new Guid(deviceEntity.SecurityDeviceUuid);
            retVal.ManufacturerModelName = deviceEntity.ManufacturerModelName;
            retVal.OperatingSystemName = deviceEntity.OperatingSystemName;
            //retVal.LoadAssociations(context);

            return retVal;
        }

        /// <summary>
        /// Insert the specified device entity
        /// </summary>
        protected override DeviceEntity InsertInternal(SQLiteDataContext context, DeviceEntity data)
        {
            if (data.SecurityDevice != null) data.SecurityDevice = data.SecurityDevice?.EnsureExists(context);
            data.SecurityDeviceKey = data.SecurityDevice?.Key ?? data.SecurityDeviceKey;

            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Updates the specified user
        /// </summary>
        protected override DeviceEntity UpdateInternal(SQLiteDataContext context, DeviceEntity data)
        {
            if (data.SecurityDevice != null) data.SecurityDevice = data.SecurityDevice?.EnsureExists(context);
            data.SecurityDeviceKey = data.SecurityDevice?.Key ?? data.SecurityDeviceKey;
            return base.UpdateInternal(context, data);
        }
    }
}
