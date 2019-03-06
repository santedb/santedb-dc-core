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
 * User: justi
 * Date: 2019-1-12
 */
using SanteDB.Core.Data.QueryBuilder;
using SanteDB.Core.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Manufactured material persistence service
    /// </summary>
    public class ManufacturedMaterialPersistenceService : IdentifiedPersistenceService<ManufacturedMaterial, DbManufacturedMaterial, DbManufacturedMaterial.QueryResult>
    {
        // Material persister
        private MaterialPersistenceService m_materialPersister = new MaterialPersistenceService();

        /// <summary>
        /// Material persister
        /// </summary>
        /// <param name="dataInstance"></param>
        /// <param name="context"></param>
        /// <param name="principal"></param>
        /// <returns></returns>
        public override ManufacturedMaterial ToModelInstance(object dataInstance, SQLiteDataContext context)
        {

            var iddat = dataInstance as DbIdentified;
            var domainMmat = dataInstance as DbManufacturedMaterial ?? dataInstance.GetInstanceOf<DbManufacturedMaterial>() ?? context.Connection.Table<DbManufacturedMaterial>().Where(o => o.Uuid == iddat.Uuid).First();
            var domainMat = dataInstance as DbMaterial ?? dataInstance.GetInstanceOf<DbMaterial>() ?? context.Connection.Table<DbMaterial>().Where(o => o.Uuid == iddat.Uuid).First();
            //var dbm = domainMat ?? context.Table<DbMaterial>().Where(o => o.Uuid == domainMmat.Uuid).First();
            var retVal = this.m_materialPersister.ToModelInstance<ManufacturedMaterial>(domainMat, context);
            retVal.LotNumber = domainMmat.LotNumber;
            return retVal;

        }

        /// <summary>
        /// Insert the specified manufactured material
        /// </summary>
        protected override ManufacturedMaterial InsertInternal(SQLiteDataContext context, ManufacturedMaterial data)
        {
            var retVal = this.m_materialPersister.Insert(context, data);
            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Updates the manufactured material
        /// </summary>
        protected override ManufacturedMaterial UpdateInternal(SQLiteDataContext context, ManufacturedMaterial data)
        {
            var updated = this.m_materialPersister.Update(context, data);
            return base.UpdateInternal(context, data);
        }

        /// <summary>
        /// Obsolete the specified manufactured material
        /// </summary>
        protected override ManufacturedMaterial ObsoleteInternal(SQLiteDataContext context, ManufacturedMaterial data)
        {
            var obsoleted = this.m_materialPersister.Obsolete(context, data);
            return data;
        }
    }
}
