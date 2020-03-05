/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
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
using SanteDB.Core.Model.Acts;
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents a persister which is a act relationship 
    /// </summary>
    public class ActRelationshipPersistenceService : IdentifiedPersistenceService<ActRelationship, DbActRelationship>, ISQLiteAssociativePersistenceService
    {

        /// <summary>
        /// Create DbActParticipation from modelinstance
        /// </summary>
        public override object FromModelInstance(ActRelationship modelInstance, SQLiteDataContext context)
        {
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbActRelationship()
            {
                SourceUuid = modelInstance.SourceEntityKey?.ToByteArray(),
                TargetUuid = modelInstance.TargetActKey?.ToByteArray(),
                RelationshipTypeUuid = modelInstance.RelationshipTypeKey?.ToByteArray(),
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
        protected override ActRelationship InsertInternal(SQLiteDataContext context, ActRelationship data)
        {
            // Ensure we haven't already persisted this
            if (data.TargetAct != null) data.TargetAct = data.TargetAct.EnsureExists(context);
            data.TargetActKey = data.TargetAct?.Key ?? data.TargetActKey;
            if (data.RelationshipType != null) data.RelationshipType = data.RelationshipType.EnsureExists(context);
            data.RelationshipTypeKey = data.RelationshipType?.Key ?? data.RelationshipTypeKey;

            byte[] target = data.TargetActKey.Value.ToByteArray(),
                source = data.SourceEntityKey.Value.ToByteArray(),
                typeKey = data.RelationshipTypeKey.Value.ToByteArray();

            //SqlStatement sql = new SqlStatement<DbActRelationship>().SelectFrom()
            //    .Where<DbActRelationship>(o => o.SourceUuid == source)
            //    .Limit(1).Build();

            //IEnumerable<DbActRelationship> dbrelationships = context.TryGetData($"EX:{sql.ToString()}") as IEnumerable<DbActRelationship>;
            //if (dbrelationships == null)
            //{
            //    dbrelationships = context.Connection.Query<DbActRelationship>(sql.SQL, sql.Arguments.ToArray()).ToList();
            //    context.AddData($"EX{sql.ToString()}", dbrelationships);
            //}
            //var existing = dbrelationships.FirstOrDefault(
            //        o => o.RelationshipTypeUuid == typeKey &&
            //        o.TargetUuid == target);

            //if (existing == null)
            //{
            return base.InsertInternal(context, data);
            //    (dbrelationships as List<DbActRelationship>).Add(new DbActRelationship()
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
        protected override ActRelationship UpdateInternal(SQLiteDataContext context, ActRelationship data)
        {
            if (data.TargetAct != null) data.TargetAct = data.TargetAct.EnsureExists(context);
            data.TargetActKey = data.TargetAct?.Key ?? data.TargetActKey;
            if (data.RelationshipType != null) data.RelationshipType = data.RelationshipType.EnsureExists(context);
            data.RelationshipTypeKey = data.RelationshipType?.Key ?? data.RelationshipTypeKey;

            return base.UpdateInternal(context, data);
        }

        /// <summary>
        /// Comparer for entity relationships
        /// </summary>
        internal class Comparer : IEqualityComparer<ActRelationship>
        {
            /// <summary>
            /// Determine equality between the two relationships
            /// </summary>
            public bool Equals(ActRelationship x, ActRelationship y)
            {
                return x.SourceEntityKey == y.SourceEntityKey &&
                    x.TargetActKey == y.TargetActKey &&
                    (x.RelationshipTypeKey == y.RelationshipTypeKey || x.RelationshipType?.Mnemonic == y.RelationshipType?.Mnemonic);
            }

            /// <summary>
            /// Get hash code
            /// </summary>
            public int GetHashCode(ActRelationship obj)
            {
                int result = obj.SourceEntityKey.GetHashCode();
                result = 37 * result + obj.RelationshipTypeKey.GetHashCode();
                result = 37 * result + obj.TargetActKey.GetHashCode();
                result = 37 * result + (obj.RelationshipType?.Mnemonic.GetHashCode() ?? 0);
                return result;
            }
        }
    }
}
