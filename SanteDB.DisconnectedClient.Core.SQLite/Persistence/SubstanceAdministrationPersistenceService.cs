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
using SanteDB.Core.Model.Acts;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents a persistence service for substance administrations
    /// </summary>
    public class SubstanceAdministrationPersistenceService : ActDerivedPersistenceService<SubstanceAdministration, DbSubstanceAdministration, DbSubstanceAdministration.QueryResult>
    {

        /// <summary>
        /// Create from model instance
        /// </summary>
        public override object FromModelInstance(SubstanceAdministration modelInstance, SQLiteDataContext context)
        {
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbSubstanceAdministration()
            {
                DoseQuantity = modelInstance.DoseQuantity,
                DoseUnitConceptUuid = modelInstance.DoseUnitKey?.ToByteArray(),
                RouteConceptUuid = modelInstance.RouteKey?.ToByteArray(),
                SequenceId = modelInstance.SequenceId,
                SiteConceptUuid = modelInstance.SiteKey?.ToByteArray(),
                Uuid = modelInstance.Key?.ToByteArray()
            };
        }

        /// <summary>
        /// Convert databased model to model
        /// </summary>
        public override SubstanceAdministration ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var iddat = dataInstance as DbIdentified;
            var dbSbadm = dataInstance as DbSubstanceAdministration ?? dataInstance.GetInstanceOf<DbSubstanceAdministration>() ?? context.Connection.Table<DbSubstanceAdministration>().Where(o => o.Uuid == iddat.Uuid).First();
            var dba = dataInstance.GetInstanceOf<DbAct>() ?? dataInstance as DbAct ?? context.Connection.Table<DbAct>().Where(a => a.Uuid == dbSbadm.Uuid).First();
            var retVal = m_actPersister.ToModelInstance<SubstanceAdministration>(dba, context);

            if (dbSbadm.DoseUnitConceptUuid != null)
                retVal.DoseUnitKey = new Guid(dbSbadm.DoseUnitConceptUuid);
            if (dbSbadm.RouteConceptUuid != null)
                retVal.RouteKey = new Guid(dbSbadm.RouteConceptUuid);
            retVal.DoseQuantity = dbSbadm.DoseQuantity;
            retVal.SequenceId = (int)dbSbadm.SequenceId;
            if (dbSbadm.SiteConceptUuid != null)
                retVal.SiteKey = new Guid(dbSbadm.SiteConceptUuid);
            return retVal;
        }

        /// <summary>
        /// Insert the specified sbadm
        /// </summary>
        protected override SubstanceAdministration InsertInternal(SQLiteDataContext context, SubstanceAdministration data)
        {
            if (data.DoseUnit != null) data.DoseUnit = data.DoseUnit?.EnsureExists(context);
            if (data.Route != null) data.Route = data.Route?.EnsureExists(context);
            data.DoseUnitKey = data.DoseUnit?.Key ?? data.DoseUnitKey;
            data.RouteKey = data.Route?.Key ?? data.RouteKey;
            return base.InsertInternal(context, data);
        }


        /// <summary>
        /// Insert the specified sbadm
        /// </summary>
        protected override SubstanceAdministration UpdateInternal(SQLiteDataContext context, SubstanceAdministration data)
        {
            if (data.DoseUnit != null) data.DoseUnit = data.DoseUnit?.EnsureExists(context);
            if (data.Route != null) data.Route = data.Route?.EnsureExists(context);
            data.DoseUnitKey = data.DoseUnit?.Key ?? data.DoseUnitKey;
            data.RouteKey = data.Route?.Key ?? data.RouteKey;
            return base.UpdateInternal(context, data);
        }
    }
}