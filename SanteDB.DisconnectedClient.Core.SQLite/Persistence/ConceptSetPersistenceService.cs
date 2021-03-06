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
using System.Linq;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Persistence service for ConceptSets
    /// </summary>
    public class ConceptSetPersistenceService : IdentifiedPersistenceService<ConceptSet, DbConceptSet>
    {

        /// <summary>
        /// Convert the concept set to model
        /// </summary>
        public override ConceptSet ToModelInstance(object dataInstance, SQLiteDataContext context)
        {

            var modelInstance = base.ToModelInstance(dataInstance, context);

            // Set the concepts
            var dbInstance = dataInstance as DbConceptSet;
            ConceptPersistenceService cps = new ConceptPersistenceService();

            modelInstance.Concepts = context.Connection.Query<DbConcept>("SELECT concept.* FROM concept_concept_set INNER JOIN concept ON (concept_concept_set.concept_uuid = concept.uuid) WHERE concept_concept_set.concept_set_uuid = ?", dbInstance.Uuid).Select(
                o => cps.ToModelInstance(o, context)
            ).ToList();

            //modelInstance.LoadAssociations(context);

            return modelInstance;
        }

        /// <summary>
        /// Insert the specified concept
        /// </summary>
        protected override ConceptSet InsertInternal(SQLiteDataContext context, ConceptSet data)
        {
            // Concept set insertion
            var retVal = base.InsertInternal(context, data);

            // Concept members (nb: this is only a UUID if from the wire)
            if (data.ConceptsXml != null)
                foreach (var r in data.ConceptsXml)
                {
                    context.Connection.Insert(new DbConceptSetConceptAssociation()
                    {
                        Uuid = Guid.NewGuid().ToByteArray(),
                        ConceptSetUuid = retVal.Key.Value.ToByteArray(),
                        ConceptUuid = r.ToByteArray()
                    });
                }

            return retVal;
        }

        /// <summary>
        /// Update the specified data elements
        /// </summary>
        protected override ConceptSet UpdateInternal(SQLiteDataContext context, ConceptSet data)
        {
            var retVal = base.UpdateInternal(context, data);
            var keyuuid = retVal.Key.Value.ToByteArray();

            // Wipe and re-associate
            if (data.ConceptsXml != null)
            {
                context.Connection.Table<DbConceptSetConceptAssociation>().Delete(o => o.ConceptSetUuid == keyuuid);
                foreach (var r in data.ConceptsXml)
                {
                    context.Connection.Insert(new DbConceptSetConceptAssociation()
                    {
                        Uuid = Guid.NewGuid().ToByteArray(),
                        ConceptSetUuid = retVal.Key.Value.ToByteArray(),
                        ConceptUuid = r.ToByteArray()
                    });
                }
            }

            return retVal;
        }
    }
}

