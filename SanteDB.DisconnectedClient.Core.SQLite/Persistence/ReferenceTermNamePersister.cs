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
using SanteDB.Core;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Persistence service for reftermname
    /// </summary>
    public class ReferenceTermNamePersister : IdentifiedPersistenceService<ReferenceTermName, DbReferenceTermName>
    {

        /// <summary>
        /// Performs the actual insert.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        /// <param name="principal">The principal.</param>
        /// <returns>Returns the inserted reference term name.</returns>
        protected override ReferenceTermName InsertInternal(SQLiteDataContext context, ReferenceTermName data)
        {
            // set the key if we don't have one
            if (!data.Key.HasValue || data.Key == Guid.Empty)
                data.Key = Guid.NewGuid();

            // set the creation time if we don't have one
            if (data.CreationTime == default(DateTimeOffset))
                data.CreationTime = DateTimeOffset.Now;

            return base.InsertInternal(context, data);
        }
    }
}
