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
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Entity relationship persistence service
    /// </summary>
    public class EntityRelationshipPersistenceService : IdentifiedPersistenceService<EntityRelationship, DbEntityRelationship>, ISQLiteAssociativePersistenceService
    {

        /// <summary>
        /// Role dictionary
        /// </summary>
        private Dictionary<Guid, String> m_relationshipMnemonicDictionary = new Dictionary<Guid, string>();

        /// <summary>
        /// Load relationship mnemonics
        /// </summary>
        public string GetRelationshipMnemonic(SQLiteDataContext context, Guid id)
        {
            if (this.m_relationshipMnemonicDictionary.Count == 0)
                lock (this.m_relationshipMnemonicDictionary)
                    if (this.m_relationshipMnemonicDictionary.Count == 0)
                        foreach (var itm in context.Connection.Query<DbConcept>("select concept.uuid, mnemonic from concept_concept_set inner join concept on (concept.uuid = concept_concept_set.concept_uuid) where concept_concept_set.concept_set_uuid = ?", ConceptSetKeys.EntityRelationshipType.ToByteArray()))
                            this.m_relationshipMnemonicDictionary.Add(itm.Key, itm.Mnemonic);
            String retVal = null;
            this.m_relationshipMnemonicDictionary.TryGetValue(id, out retVal);
            return retVal;
        }

        /// <summary>
        /// To model instance 
        /// </summary>
        public override EntityRelationship ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var dbi = dataInstance as DbEntityRelationship;
            if (dbi == null) return null;

            var roleKey = new Guid(dbi.RelationshipTypeUuid);
            return new EntityRelationship()
            {
                SourceEntityKey = new Guid(dbi.SourceUuid),
                LoadState = SanteDB.Core.Model.LoadState.FullLoad,
                RelationshipTypeKey = roleKey,
                RelationshipType = new Concept()
                {
                    Key = roleKey,
                    Mnemonic = this.GetRelationshipMnemonic(context, roleKey)
                },
                TargetEntityKey = new Guid(dbi.TargetUuid),
                Quantity = dbi.Quantity,
                Key = new Guid(dbi.Uuid)
            };
        }
        /// <summary>
        /// From model instance
        /// </summary>
        public override object FromModelInstance(EntityRelationship modelInstance, SQLiteDataContext context)
        {
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbEntityRelationship()
            {
                Quantity = modelInstance.Quantity,
                RelationshipTypeUuid = modelInstance.RelationshipTypeKey?.ToByteArray(),
                SourceUuid = modelInstance.SourceEntityKey?.ToByteArray(),
                TargetUuid = modelInstance.TargetEntityKey?.ToByteArray(),
                Uuid = modelInstance.Key?.ToByteArray()
            };
        }

        /// <summary>
        /// Get from source
        /// </summary>
        public IEnumerable GetFromSource(SQLiteDataContext context, Guid id, decimal? versionSequenceId)
        {
            return this.Query(context, o => o.SourceEntityKey == id);
        }

        /// <summary>
        /// Insert the relationship
        /// </summary>
        protected override EntityRelationship InsertInternal(SQLiteDataContext context, EntityRelationship data)
        {

            // Ensure we haven't already persisted this
            data.TargetEntityKey = data.TargetEntity?.Key ?? data.TargetEntityKey;
            if (data.RelationshipType != null) data.RelationshipType = data.RelationshipType.EnsureExists(context, false);
            data.RelationshipTypeKey = data.RelationshipType?.Key ?? data.RelationshipTypeKey;

            //byte[] target = data.TargetEntityKey.Value.ToByteArray(),
            //    source = data.SourceEntityKey.Value.ToByteArray(),
            //    typeKey = data.RelationshipTypeKey.Value.ToByteArray();

            //SqlStatement sql = new SqlStatement<DbEntityRelationship>().SelectFrom()
            //    .Where<DbEntityRelationship>(o => o.SourceUuid == source)
            //    .Limit(1).Build();

            //IEnumerable<DbEntityRelationship> dbrelationships = context.TryGetData($"EX:{sql.ToString()}") as IEnumerable<DbEntityRelationship>;
            //if (dbrelationships == null) { 
            //    dbrelationships = context.Connection.Query<DbEntityRelationship>(sql.SQL, sql.Arguments.ToArray()).ToList();
            //                    context.AddData($"EX{sql.ToString()}", dbrelationships);
            //}

            //var existing = dbrelationships.FirstOrDefault(
            //        o => o.RelationshipTypeUuid == data.RelationshipTypeKey.Value.ToByteArray() &&
            //        o.TargetUuid == data.TargetEntityKey.Value.ToByteArray());

            //if (existing == null)
            //{
            return base.InsertInternal(context, data);
            //    (dbrelationships as List<DbEntityRelationship>).Add(new DbEntityRelationship()
            //    {
            //        Uuid = retVal.Key.Value.ToByteArray(),
            //        RelationshipTypeUuid = typeKey,
            //        SourceUuid = source,
            //        TargetUuid = target
            //    });
            //    return retVal;
            //}
            //else
            //{
            //    data.Key = new Guid(existing.Uuid);
            //    return data;
            //}
        }

        /// <summary>
        /// Update the specified object
        /// </summary>
        protected override EntityRelationship UpdateInternal(SQLiteDataContext context, EntityRelationship data)
        {
            // Ensure we haven't already persisted this
            //if (data.TargetEntity != null) data.TargetEntity = data.TargetEntity.EnsureExists(context);
            data.TargetEntityKey = data.TargetEntity?.Key ?? data.TargetEntityKey;
            if (data.RelationshipType != null) data.RelationshipType = data.RelationshipType.EnsureExists(context, false);
            data.RelationshipTypeKey = data.RelationshipType?.Key ?? data.RelationshipTypeKey;
            return base.UpdateInternal(context, data);
        }

        /// <summary>
        /// Comparer for entity relationships
        /// </summary>
        internal class Comparer : IEqualityComparer<EntityRelationship>
        {
            /// <summary>
            /// Determine equality between the two relationships
            /// </summary>
            public bool Equals(EntityRelationship x, EntityRelationship y)
            {
                return x.SourceEntityKey == y.SourceEntityKey &&
                    x.TargetEntityKey == y.TargetEntityKey &&
                    (x.RelationshipTypeKey == y.RelationshipTypeKey || x.RelationshipType?.Mnemonic == y.RelationshipType?.Mnemonic);
            }

            /// <summary>
            /// Get hash code
            /// </summary>
            public int GetHashCode(EntityRelationship obj)
            {
                int result = obj.SourceEntityKey.GetValueOrDefault().GetHashCode();
                result = 37 * result + obj.RelationshipTypeKey.GetValueOrDefault().GetHashCode();
                result = 37 * result + obj.TargetEntityKey.GetValueOrDefault().GetHashCode();
                result = 37 * result + (obj.RelationshipTypeKey ?? obj.RelationshipType?.Key).GetHashCode();
                return result;
            }
        }
    }
}
