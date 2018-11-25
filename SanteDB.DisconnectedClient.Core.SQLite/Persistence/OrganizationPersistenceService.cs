/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-6-28
 */
using SanteDB.Core.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents an organization persistence service
    /// </summary>
    public class OrganizationPersistenceService : EntityDerivedPersistenceService<Organization, DbOrganization, DbOrganization.QueryResult>
    {


        /// <summary>
        /// Model instance
        /// </summary>
        public override Organization ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var iddat = dataInstance as DbVersionedData;
            var organization = dataInstance as DbOrganization ?? dataInstance.GetInstanceOf<DbOrganization>() ?? context.Connection.Table<DbOrganization>().Where(o => o.Uuid == iddat.Uuid).First();
            var dbe = dataInstance.GetInstanceOf<DbEntity>() ?? dataInstance as DbEntity ?? context.Connection.Table<DbEntity>().Where(o => o.Uuid == organization.Uuid).First();
            var retVal = m_entityPersister.ToModelInstance<Organization>(dbe, context);
            retVal.IndustryConceptKey = organization.IndustryConceptUuid != null ? (Guid?)new Guid(organization.IndustryConceptUuid) : null;
            return retVal;
        }

        /// <summary>
        /// Insert the organization
        /// </summary>
        protected override Organization InsertInternal(SQLiteDataContext context, Organization data)
        {
            // ensure industry concept exists
            if (data.IndustryConcept != null) data.IndustryConcept = data.IndustryConcept?.EnsureExists(context);
            data.IndustryConceptKey = data.IndustryConcept?.Key ?? data.IndustryConceptKey;

            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Update the organization
        /// </summary>
        protected override Organization UpdateInternal(SQLiteDataContext context, Organization data)
        {
            if (data.IndustryConcept != null) data.IndustryConcept = data.IndustryConcept?.EnsureExists(context);
            data.IndustryConceptKey = data.IndustryConcept?.Key ?? data.IndustryConceptKey;
            return base.UpdateInternal(context, data);
        }

    }
}
