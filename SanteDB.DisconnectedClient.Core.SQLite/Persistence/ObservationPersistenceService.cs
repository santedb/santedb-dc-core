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
    /// Persistence class for observations
    /// </summary>
    public abstract class ObservationPersistenceService<TObservation, TDbObservation, TQueryResult> : ActDerivedPersistenceService<TObservation, TDbObservation, TQueryResult>
        where TObservation : Observation, new()
        where TDbObservation : DbIdentified, new()
        where TQueryResult : DbIdentified
    {

        /// <summary>
        /// From model instance
        /// </summary>
        public override object FromModelInstance(TObservation modelInstance, SQLiteDataContext context)
        {
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbObservation()
            {
                InterpretationConceptUuid = modelInstance.InterpretationConceptKey?.ToByteArray(),
                Uuid = modelInstance.Key?.ToByteArray(),
                ValueType = modelInstance is TextObservation ? "ST" : modelInstance is CodedObservation ? "CD" : "PQ"
            };
        }

        /// <summary>
        /// Convert a data act and observation instance to an observation
        /// </summary>
        public virtual TObservation ToModelInstance(TDbObservation dataInstance, DbAct actInstance, DbObservation obsInstance, SQLiteDataContext context)
        {
            var retVal = m_actPersister.ToModelInstance<TObservation>(actInstance, context);

            if (obsInstance.InterpretationConceptUuid != null)
                retVal.InterpretationConceptKey = new Guid(obsInstance.InterpretationConceptUuid);

            return retVal;
        }

        /// <summary>
        /// Insert the specified observation into the database
        /// </summary>
        protected override TObservation InsertInternal(SQLiteDataContext context, TObservation data)
        {
            if (data.InterpretationConcept != null) data.InterpretationConcept = data.InterpretationConcept?.EnsureExists(context);
            data.InterpretationConceptKey = data.InterpretationConcept?.Key ?? data.InterpretationConceptKey;

            var inserted = base.InsertInternal(context, data);

            // Not pure observation
            if (data.GetType() != typeof(Observation))
            {
                var dbobservation = new DbObservation()
                {
                    InterpretationConceptUuid = data.InterpretationConceptKey?.ToByteArray(),
                    Uuid = inserted.Key?.ToByteArray()
                };
                // Value type
                if (data is QuantityObservation)
                    dbobservation.ValueType = "PQ";
                else if (data is TextObservation)
                    dbobservation.ValueType = "ST";
                else if (data is CodedObservation)
                    dbobservation.ValueType = "CD";

                // Persist
                context.Connection.Insert(dbobservation);
            }
            return inserted;
        }

        /// <summary>
        /// Updates the specified observation
        /// </summary>
        protected override TObservation UpdateInternal(SQLiteDataContext context, TObservation data)
        {
            if (data.InterpretationConcept != null) data.InterpretationConcept = data.InterpretationConcept?.EnsureExists(context);
            data.InterpretationConceptKey = data.InterpretationConcept?.Key ?? data.InterpretationConceptKey;

            var updated = base.UpdateInternal(context, data);

            // Not pure observation
            var dbobservation = context.Connection.Get<DbObservation>(data.Key?.ToByteArray());
            dbobservation.InterpretationConceptUuid = data.InterpretationConceptKey?.ToByteArray();
            context.Connection.Update(dbobservation);
            return updated;
        }
    }

    /// <summary>
    /// Text observation service
    /// </summary>
    public class TextObservationPersistenceService : ObservationPersistenceService<TextObservation, DbTextObservation, DbTextObservation.QueryResult>
    {
        /// <summary>
        /// From model instance
        /// </summary>
        public override object FromModelInstance(TextObservation modelInstance, SQLiteDataContext context)
        {
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbTextObservation()
            {
                Uuid = modelInstance.Key?.ToByteArray(),
                Value = modelInstance.Value
            };
        }

        /// <summary>
        /// Convert the specified object to a model instance
        /// </summary>
        public override TextObservation ToModelInstance(DbTextObservation dataInstance, DbAct actInstance, DbObservation obsInstance, SQLiteDataContext context)
        {
            var retVal = base.ToModelInstance(dataInstance, actInstance, obsInstance, context);
            retVal.Value = dataInstance.Value;
            return retVal;
        }

        /// <summary>
        /// Convert to model instance
        /// </summary>
        public override TextObservation ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var iddat = dataInstance as DbVersionedData;
            var textObs = dataInstance as DbTextObservation ?? dataInstance.GetInstanceOf<DbTextObservation>() ?? context.Connection.Table<DbTextObservation>().Where(o => o.Uuid == iddat.Uuid).First();
            var dba = dataInstance.GetInstanceOf<DbAct>() ?? dataInstance as DbAct ?? context.Connection.Table<DbAct>().Where(o => o.Uuid == iddat.Uuid).First();
            var dbo = dataInstance.GetInstanceOf<DbObservation>() ?? context.Connection.Table<DbObservation>().Where(o => o.Uuid == iddat.Uuid).First();
            return this.ToModelInstance(textObs, dba, dbo, context);
        }
    }

    /// <summary>
    /// Coded observation service
    /// </summary>
    public class CodedObservationPersistenceService : ObservationPersistenceService<CodedObservation, DbCodedObservation, DbCodedObservation.QueryResult>
    {
        /// <summary>
        /// From model instance
        /// </summary>
        public override object FromModelInstance(CodedObservation modelInstance, SQLiteDataContext context)
        {
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbCodedObservation()
            {
                Uuid = modelInstance.Key?.ToByteArray(),
                Value = modelInstance.ValueKey.ToByteArray()
            };
        }

        /// <summary>
        /// Convert the specified object to a model instance
        /// </summary>
        public override CodedObservation ToModelInstance(DbCodedObservation dataInstance, DbAct actInstance, DbObservation obsInstance, SQLiteDataContext context)
        {
            var retVal = base.ToModelInstance(dataInstance, actInstance, obsInstance, context);
            if (dataInstance.Value != null)
                retVal.ValueKey = new Guid(dataInstance.Value);
            return retVal;
        }

        /// <summary>
        /// Convert to model instance
        /// </summary>
        public override CodedObservation ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var iddat = dataInstance as DbVersionedData;
            var codeObs = dataInstance as DbCodedObservation ?? dataInstance.GetInstanceOf<DbCodedObservation>() ?? context.Connection.Table<DbCodedObservation>().Where(o => o.Uuid == iddat.Uuid).First();
            var dba = dataInstance.GetInstanceOf<DbAct>() ?? dataInstance as DbAct ?? context.Connection.Table<DbAct>().Where(o => o.Uuid == iddat.Uuid).First();
            var dbo = dataInstance.GetInstanceOf<DbObservation>() ?? context.Connection.Table<DbObservation>().Where(o => o.Uuid == iddat.Uuid).First();
            return this.ToModelInstance(codeObs, dba, dbo, context);
        }

        /// <summary>
        /// Insert the observation
        /// </summary>
        protected override CodedObservation InsertInternal(SQLiteDataContext context, CodedObservation data)
        {
            if (data.Value != null) data.Value = data.Value?.EnsureExists(context);
            data.ValueKey = data.Value?.Key ?? data.ValueKey;
            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Update the specified observation
        /// </summary>
        protected override CodedObservation UpdateInternal(SQLiteDataContext context, CodedObservation data)
        {
            if (data.Value != null) data.Value = data.Value?.EnsureExists(context);
            data.ValueKey = data.Value?.Key ?? data.ValueKey;
            return base.UpdateInternal(context, data);
        }
    }

    /// <summary>
    /// Quantity observation persistence service
    /// </summary>
    public class QuantityObservationPersistenceService : ObservationPersistenceService<QuantityObservation, DbQuantityObservation, DbQuantityObservation.QueryResult>
    {

        /// <summary>
        /// From model instance
        /// </summary>
        public override object FromModelInstance(QuantityObservation modelInstance, SQLiteDataContext context)
        {
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbQuantityObservation()
            {
                UnitOfMeasureUuid = modelInstance.UnitOfMeasureKey?.ToByteArray(),
                Uuid = modelInstance.Key?.ToByteArray(),
                Value = modelInstance.Value
            };
        }

        /// <summary>
        /// Convert the specified object to a model instance
        /// </summary>
        public override QuantityObservation ToModelInstance(DbQuantityObservation dataInstance, DbAct actInstance, DbObservation obsInstance, SQLiteDataContext context)
        {
            var retVal = base.ToModelInstance(dataInstance, actInstance, obsInstance, context);
            if (dataInstance.UnitOfMeasureUuid != null)
                retVal.UnitOfMeasureKey = new Guid(dataInstance.UnitOfMeasureUuid);
            retVal.Value = dataInstance.Value;
            return retVal;
        }

        /// <summary>
        /// Convert to model instance
        /// </summary>
        public override QuantityObservation ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var iddat = dataInstance as DbVersionedData;
            var qObs = dataInstance as DbQuantityObservation ?? dataInstance.GetInstanceOf<DbQuantityObservation>() ?? context.Connection.Table<DbQuantityObservation>().Where(o => o.Uuid == iddat.Uuid).First();
            var dba = dataInstance.GetInstanceOf<DbAct>() ?? dataInstance as DbAct ?? context.Connection.Table<DbAct>().Where(o => o.Uuid == qObs.Uuid).First();
            var dbo = dataInstance.GetInstanceOf<DbObservation>() ?? context.Connection.Table<DbObservation>().Where(o => o.Uuid == qObs.Uuid).First();
            return this.ToModelInstance(qObs, dba, dbo, context);
        }

        /// <summary>
        /// Insert the observation
        /// </summary>
        protected override QuantityObservation InsertInternal(SQLiteDataContext context, QuantityObservation data)
        {
            if (data.UnitOfMeasure != null) data.UnitOfMeasure = data.UnitOfMeasure?.EnsureExists(context);
            data.UnitOfMeasureKey = data.UnitOfMeasure?.Key ?? data.UnitOfMeasureKey;
            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Update the specified observation
        /// </summary>
        protected override QuantityObservation UpdateInternal(SQLiteDataContext context, QuantityObservation data)
        {
            if (data.UnitOfMeasure != null) data.UnitOfMeasure = data.UnitOfMeasure?.EnsureExists(context);
            data.UnitOfMeasureKey = data.UnitOfMeasure?.Key ?? data.UnitOfMeasureKey;
            return base.UpdateInternal(context, data);
        }
    }
}