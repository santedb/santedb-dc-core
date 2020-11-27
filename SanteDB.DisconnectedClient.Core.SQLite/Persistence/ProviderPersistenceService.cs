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
using SanteDB.Core.Model.Roles;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Roles;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Provider persistence service
    /// </summary>
    public class ProviderPersistenceService : IdentifiedPersistenceService<Provider, DbProvider, DbProvider.QueryResult>
    {
        // Entity persisters
        private PersonPersistenceService m_personPersister = new PersonPersistenceService();
        protected EntityPersistenceService m_entityPersister = new EntityPersistenceService();

        /// <summary>
        /// Model instance
        /// </summary>
        public override Provider ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var iddat = dataInstance as DbVersionedData;
            var provider = dataInstance as DbProvider ?? dataInstance.GetInstanceOf<DbProvider>() ?? context.Connection.Table<DbProvider>().Where(o => o.Uuid == iddat.Uuid).First();
            var dbe = dataInstance.GetInstanceOf<DbEntity>() ?? dataInstance as DbEntity ?? context.Connection.Table<DbEntity>().Where(o => o.Uuid == provider.Uuid).First();
            var dbp = context.Connection.Table<DbPerson>().Where(o => o.Uuid == provider.Uuid).First();
            var retVal = m_entityPersister.ToModelInstance<Provider>(dbe, context);

            retVal.DateOfBirth = dbp.DateOfBirth;
            // Reverse lookup
            // Reverse lookup
            if (!String.IsNullOrEmpty(dbp.DateOfBirthPrecision))
                retVal.DateOfBirthPrecision = PersonPersistenceService.PrecisionMap.Where(o => o.Value == dbp.DateOfBirthPrecision).Select(o => o.Key).First();
            retVal.ProviderSpecialtyKey = provider.Specialty == null ? null : (Guid?)new Guid(provider.Specialty);
            //retVal.LoadAssociations(context);

            return retVal;
        }

        /// <summary>
        /// Insert the specified person into the database
        /// </summary>
        protected override Provider InsertInternal(SQLiteDataContext context, Provider data)
        {
            if (data.ProviderSpecialty != null) data.ProviderSpecialty?.EnsureExists(context);
            data.ProviderSpecialtyKey = data.ProviderSpecialty?.Key ?? data.ProviderSpecialtyKey;

            var inserted = this.m_personPersister.Insert(context, data);
            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Update the specified person
        /// </summary>
        protected override Provider UpdateInternal(SQLiteDataContext context, Provider data)
        {
            // Ensure exists
            if (data.ProviderSpecialty != null) data.ProviderSpecialty = data.ProviderSpecialty?.EnsureExists(context);
            data.ProviderSpecialtyKey = data.ProviderSpecialty?.Key ?? data.ProviderSpecialtyKey;

            this.m_personPersister.Update(context, data);
            return base.UpdateInternal(context, data);
        }

        /// <summary>
        /// Obsolete the object
        /// </summary>
        protected override Provider ObsoleteInternal(SQLiteDataContext context, Provider data)
        {
            var retVal = this.m_personPersister.Obsolete(context, data);
            return data;
        }

    }
}
