/*
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
using SanteDB.Core.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Represents a persister which persists places
    /// </summary>
    public class PlacePersistenceService : EntityDerivedPersistenceService<Place, DbPlace, DbPlace.QueryResult>
    {
        /// <summary>
        /// Load to a model instance
        /// </summary>
        public override Place ToModelInstance(object dataInstance, SQLiteDataContext context)
        {
            var iddat = dataInstance as DbVersionedData;
            var place = dataInstance as DbPlace ?? dataInstance.GetInstanceOf<DbPlace>() ?? context.Connection.Table<DbPlace>().Where(o => o.Uuid == iddat.Uuid).FirstOrDefault();
            var dbe = dataInstance.GetInstanceOf<DbEntity>() ?? dataInstance as DbEntity ?? context.Connection.Table<DbEntity>().Where(o => o.Uuid == place.Uuid).First();

            var retVal = m_entityPersister.ToModelInstance<Place>(dbe, context);
            if (place != null)
            {
                retVal.IsMobile = place.IsMobile;
                retVal.Lat = place.Lat;
                retVal.Lng = place.Lng;
            }
            //retVal.LoadAssociations(context);

            return retVal;
        }

        /// <summary>
        /// Insert 
        /// </summary>
        protected override Place InsertInternal(SQLiteDataContext context, Place data)
        {
            var retVal = base.InsertInternal(context, data);

            if (data.Services != null)
                base.UpdateAssociatedItems<PlaceService, Entity>(
                    new List<PlaceService>(),
                    data.Services,
                    data.Key,
                    context);

            return retVal;
        }

        /// <summary>
        /// Update the place
        /// </summary>
        protected override Place UpdateInternal(SQLiteDataContext context, Place data)
        {
            var retVal = base.UpdateInternal(context, data);

            byte[] sourceKey = data.Key.Value.ToByteArray();
            if (data.Services != null)
                base.UpdateAssociatedItems<PlaceService, Entity>(
                    context.Connection.Table<DbPlaceService>().Where(o => o.SourceUuid == sourceKey).ToList().Select(o => m_mapper.MapDomainInstance<DbPlaceService, PlaceService>(o)).ToList(),
                    data.Services,
                    data.Key,
                    context);

            return retVal;
        }
    }
}
