﻿/*
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
    /// Represents a reference term persistence service.
    /// </summary>
    public class ReferenceTermPersistenceService : BaseDataPersistenceService<ReferenceTerm, DbReferenceTerm>
    {
        /// <summary>
        /// Inserts a reference term.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        /// <param name="principal">The principal.</param>
        /// <returns>Returns the inserted reference term.</returns>
        protected override ReferenceTerm InsertInternal(SQLiteDataContext context, ReferenceTerm data)
        {
            var referenceTerm = base.InsertInternal(context, data);

            if (referenceTerm.DisplayNames != null)
            {
                base.UpdateAssociatedItems<ReferenceTermName, ReferenceTerm>(
                    new List<ReferenceTermName>(), 
                    referenceTerm.DisplayNames, 
                    data.Key, 
                    context);
            }

            return referenceTerm;
        }

        /// <summary>
        /// Updates a reference term.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        /// <param name="principal">The principal.</param>
        /// <returns>Returns the updated reference term.</returns>
        protected override ReferenceTerm UpdateInternal(SQLiteDataContext context, ReferenceTerm data)
        {
            var referenceTerm = base.UpdateInternal(context, data);

            var uuid = referenceTerm.Key.Value.ToByteArray();

            if (referenceTerm.DisplayNames != null)
            {
                base.UpdateAssociatedItems<ReferenceTermName, ReferenceTerm>(
                    context.Connection.Table<DbReferenceTermName>().Where(o => o.ReferenceTermUuid == uuid).ToList().Select(o => m_mapper.MapDomainInstance<DbReferenceTermName, ReferenceTermName>(o)).ToList(), 
                    referenceTerm.DisplayNames, 
                    data.Key, 
                    context);
            }

            return referenceTerm;
        }
    }
}
