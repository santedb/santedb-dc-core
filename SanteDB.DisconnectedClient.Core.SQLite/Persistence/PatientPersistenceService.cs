/*
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
using SanteDB.Core.Model.Roles;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Roles;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Persistence service which is used to persist patients
    /// </summary>
    public class PatientPersistenceService : IdentifiedPersistenceService<Patient, DbPatient, DbPatient.QueryResult>
    {
        // Entity persisters
        private PersonPersistenceService m_personPersister = new PersonPersistenceService();
        protected EntityPersistenceService m_entityPersister = new EntityPersistenceService();

        /// <summary>
        /// From model instance
        /// </summary>
        public override object FromModelInstance(Patient modelInstance, SQLiteDataContext context)
        {
            /*
            var dbPatient = base.FromModelInstance(modelInstance, context) as DbPatient;

            if (modelInstance.DeceasedDatePrecision.HasValue)
                dbPatient.DeceasedDatePrecision = PersonPersistenceService.PrecisionMap[modelInstance.DeceasedDatePrecision.Value];
            return dbPatient;
            */
            modelInstance.Key = modelInstance.Key ?? Guid.NewGuid();
            return new DbPatient()
            {
                Uuid = modelInstance.Key?.ToByteArray(),
                DeceasedDate = modelInstance.DeceasedDate,
                DeceasedDatePrecision = modelInstance.DeceasedDatePrecision.HasValue ? PersonPersistenceService.PrecisionMap[modelInstance.DeceasedDatePrecision.Value] : null,
                GenderConceptUuid = modelInstance.GenderConceptKey?.ToByteArray(),
                MultipleBirthOrder = modelInstance.MultipleBirthOrder,
                EducationLevelUuid = modelInstance.EducationLevelKey?.ToByteArray(),
                EthnicGroupCodeUuid = modelInstance.EthnicGroupCodeKey?.ToByteArray(),
                LivingArrangementUuid = modelInstance.LivingArrangementKey?.ToByteArray(),
                MaritalStatusUuid = modelInstance.MaritalStatusKey?.ToByteArray(),
                VipStatusUuid = modelInstance.VipStatusKey?.ToByteArray()
            };
        }

        /// <summary>
        /// Model instance
        /// </summary>
        public override Patient ToModelInstance(object dataInstance, SQLiteDataContext context)
        {

            var iddat = dataInstance as DbVersionedData;
            var patient = dataInstance as DbPatient ?? dataInstance.GetInstanceOf<DbPatient>() ?? context.Connection.Table<DbPatient>().Where(o => o.Uuid == iddat.Uuid).First();
            var dbe = dataInstance.GetInstanceOf<DbEntity>() ?? dataInstance as DbEntity ?? context.Connection.Table<DbEntity>().Where(o => o.Uuid == patient.Uuid).First();
            var dbp = dataInstance.GetInstanceOf<DbPerson>() ?? context.Connection.Table<DbPerson>().Where(o => o.Uuid == patient.Uuid).First();
            var retVal = m_entityPersister.ToModelInstance<Patient>(dbe, context);
            retVal.DateOfBirth = dbp.DateOfBirth.HasValue ? (DateTime?)dbp.DateOfBirth.Value.ToLocalTime() : null;
            // Reverse lookup
            if (!String.IsNullOrEmpty(dbp.DateOfBirthPrecision))
                retVal.DateOfBirthPrecision = PersonPersistenceService.PrecisionMap.Where(o => o.Value == dbp.DateOfBirthPrecision).Select(o => o.Key).First();
            else
                retVal.DateOfBirthPrecision = SanteDB.Core.Model.DataTypes.DatePrecision.Day;

            retVal.DeceasedDate = patient.DeceasedDate.HasValue ? (DateTime?)patient.DeceasedDate.Value.ToLocalTime() : null;
            // Reverse lookup
            if (!String.IsNullOrEmpty(patient.DeceasedDatePrecision))
                retVal.DeceasedDatePrecision = PersonPersistenceService.PrecisionMap.Where(o => o.Value == patient.DeceasedDatePrecision).Select(o => o.Key).First();
            retVal.MultipleBirthOrder = patient.MultipleBirthOrder;
            retVal.GenderConceptKey = new Guid(patient.GenderConceptUuid);

            retVal.EducationLevelKey = patient.EducationLevelUuid?.ToGuid();
            retVal.EthnicGroupCodeKey = patient.EthnicGroupCodeUuid?.ToGuid();
            retVal.LivingArrangementKey = patient.LivingArrangementUuid?.ToGuid();
            retVal.MaritalStatusKey = patient.MaritalStatusUuid?.ToGuid();
            retVal.VipStatusKey = patient.VipStatusUuid?.ToGuid();
            retVal.OccupationKey = dbp.OccupationUuid?.ToGuid();

            //retVal.LoadAssociations(context);

            return retVal;
        }

        /// <summary>
        /// Insert the specified person into the database
        /// </summary>
        protected override Patient InsertInternal(SQLiteDataContext context, Patient data)
        {
            if (data.GenderConcept != null) data.GenderConcept = data.GenderConcept?.EnsureExists(context);
            data.GenderConceptKey = data.GenderConcept?.Key ?? data.GenderConceptKey;
            if (data.VipStatus != null) data.VipStatus = data.VipStatus?.EnsureExists(context, false);
            data.VipStatusKey = data.VipStatus?.Key ?? data.VipStatusKey;

            var inserted = this.m_personPersister.Insert(context, data);
            return base.InsertInternal(context, data);
        }

        /// <summary>
        /// Update the specified person
        /// </summary>
        protected override Patient UpdateInternal(SQLiteDataContext context, Patient data)
        {
            // Ensure exists
            if (data.GenderConcept != null) data.GenderConcept = data.GenderConcept?.EnsureExists(context);
            data.GenderConceptKey = data.GenderConcept?.Key ?? data.GenderConceptKey;
            if (data.VipStatus != null) data.VipStatus = data.VipStatus?.EnsureExists(context, false);
            data.VipStatusKey = data.VipStatus?.Key ?? data.VipStatusKey;

            this.m_personPersister.Update(context, data);
            return base.UpdateInternal(context, data);
        }

        /// <summary>
        /// Obsolete the object
        /// </summary>
        protected override Patient ObsoleteInternal(SQLiteDataContext context, Patient data)
        {
            var retVal = this.m_personPersister.Obsolete(context, data);
            return data;
        }
    }
}
