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
using SanteDB.Core.Model.DataTypes;
using SanteDB.DisconnectedClient.SQLite.Model.DataType;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents a persistence service for authorities
    /// </summary>
    public class AuthorityPersistenceService : BaseDataPersistenceService<AssigningAuthority, DbAssigningAuthority>
    {

        /// <summary>
        /// Convert assigning authority to model
        /// </summary>
        public override AssigningAuthority ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var dataAA = dataInstance as DbAssigningAuthority;
            var retVal = base.ToModelInstance(dataInstance, context);
            retVal.AuthorityScopeXml = context.Connection.Table<DbAuthorityScope>().Where(o => o.AssigningAuthorityUuid == dataAA.Uuid).ToList().Select(o => new Guid(o.ScopeConceptUuid)).ToList();
            return retVal;
        }

        /// <summary>
        /// Insert the specified data
        /// </summary>
        protected override AssigningAuthority InsertInternal(SQLiteDataContext context, AssigningAuthority data)
        {
            var retVal = base.InsertInternal(context, data);

            // Scopes?
            if (retVal.AuthorityScopeXml != null)
                context.Connection.InsertAll(retVal.AuthorityScopeXml.Select(o => new DbAuthorityScope() { Key = Guid.NewGuid(), ScopeConceptUuid = o.ToByteArray(), AssigningAuthorityUuid = retVal.Key.Value.ToByteArray() }));
            return retVal;
        }

        /// <summary>
        /// Update the specified data
        /// </summary>
        protected override AssigningAuthority UpdateInternal(SQLiteDataContext context, AssigningAuthority data)
        {
            var retVal = base.UpdateInternal(context, data);
            var ruuid = retVal.Key.Value.ToByteArray();
            // Scopes?
            if (retVal.AuthorityScopeXml != null)
            {
                foreach (var itm in context.Connection.Table<DbAuthorityScope>().Where(o => o.Uuid == ruuid))
                    context.Connection.Delete(itm);
                context.Connection.InsertAll(retVal.AuthorityScopeXml.Select(o => new DbAuthorityScope() { Key = Guid.NewGuid(), ScopeConceptUuid = o.ToByteArray(), AssigningAuthorityUuid = retVal.Key.Value.ToByteArray() }));
            }
            return retVal;
        }
    }
}
