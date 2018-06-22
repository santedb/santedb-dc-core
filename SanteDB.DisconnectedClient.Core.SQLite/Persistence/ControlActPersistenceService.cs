﻿/*
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
 * User: fyfej
 * Date: 2017-9-1
 */
using SanteDB.Core.Model.Acts;
using SanteDB.DisconnectedClient.Core.Data.Model;
using SanteDB.DisconnectedClient.Core.Data.Model.Acts;
using SQLite.Net;

namespace SanteDB.DisconnectedClient.Core.Data.Persistence
{
    /// <summary>
    /// Control act persistence service
    /// </summary>
    public class ControlActPersistenceService : ActDerivedPersistenceService<ControlAct, DbControlAct, DbControlAct>
    {
        /// <summary>
        /// Convert to model instance
        /// </summary>
        public override ControlAct ToModelInstance(object dataInstance, LocalDataContext context)
        {
            var iddat = dataInstance as DbIdentified;
            var controlAct = dataInstance as DbControlAct ?? context.Connection.Table<DbControlAct>().Where(o => o.Uuid == iddat.Uuid).First();
            var dba = dataInstance as DbAct ?? context.Connection.Table<DbAct>().Where(a => a.Uuid == controlAct.Uuid).First();
            // TODO: Any other cact fields
            return m_actPersister.ToModelInstance<ControlAct>(dba, context);
        }
    }
}