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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using SanteDB.Core.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents a persister that can read/write user entities
    /// </summary>
    public class UserEntityPersistenceService : IdentifiedPersistenceService<UserEntity, DbUserEntity>
    {

        // Entity persisters
        private PersonPersistenceService m_personPersister = new PersonPersistenceService();
        protected EntityPersistenceService m_entityPersister = new EntityPersistenceService();


        /// <summary>
        /// To model instance
        /// </summary>
        public override UserEntity ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var iddat = dataInstance as DbVersionedData;
            var userEntity = dataInstance as DbUserEntity ?? dataInstance.GetInstanceOf<DbUserEntity>() ?? context.Connection.Table<DbUserEntity>().Where(o => o.Uuid == iddat.Uuid).First();
            var dbe = dataInstance.GetInstanceOf<DbEntity>() ?? dataInstance as DbEntity ?? context.Connection.Table<DbEntity>().Where(o => o.Uuid == userEntity.Uuid).First();
            var dbp = context.Connection.Table<DbPerson>().Where(o => o.Uuid == userEntity.Uuid).First();
            var retVal = m_entityPersister.ToModelInstance<UserEntity>(dbe, context);
            retVal.SecurityUserKey = new Guid(userEntity.SecurityUserUuid);
            retVal.DateOfBirth = dbp.DateOfBirth;
            // Reverse lookup
            if (!String.IsNullOrEmpty(dbp.DateOfBirthPrecision))
                retVal.DateOfBirthPrecision = PersonPersistenceService.PrecisionMap.Where(o => o.Value == dbp.DateOfBirthPrecision).Select(o => o.Key).First();

            // retVal.LoadAssociations(context);

            return retVal;
        }

        /// <summary>
        /// Inserts the user entity
        /// </summary>
        protected override UserEntity InsertInternal(SQLiteDataContext context, UserEntity data)
        {
            if (data.SecurityUser != null) data.SecurityUser = data.SecurityUser?.EnsureExists(context);
            data.SecurityUserKey = data.SecurityUser?.Key ?? data.SecurityUserKey;
            var inserted = this.m_personPersister.Insert(context, data);

            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Update the specified user entity
        /// </summary>
        protected override UserEntity UpdateInternal(SQLiteDataContext context, UserEntity data)
        {
            //            data.SecurityUser?.EnsureExists(context);
            //            data.SecurityUserKey = data.SecurityUser?.Key ?? data.SecurityUserKey;
            this.m_personPersister.Update(context, data);
            return base.UpdateInternal(context, data);
        }

        /// <summary>
        /// Obsolete the specified user instance
        /// </summary>
        protected override UserEntity ObsoleteInternal(SQLiteDataContext context, UserEntity data)
        {
            var retVal = this.m_personPersister.Obsolete(context, data);
            return data;
        }
    }
}
