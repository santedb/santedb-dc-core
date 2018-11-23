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
 * User: justin
 * Date: 2018-11-23
 */
using System;
using System.Linq;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using System.Collections.Generic;
using System.Linq.Expressions;
using SanteDB.Core.Model.Acts;
using System.Reflection;
using SanteDB.Core.Services;

namespace SanteDB.DisconnectedClient.Core.Data
{
    /// <summary>
    /// Entity source which loads objects using the IRepositoryService<> instead of IDataPersistenceService<>
    /// </summary>
    public class RepositoryEntitySource : IEntitySourceProvider
	{


        #region IEntitySourceProvider implementation

        /// <summary>
        /// Get the persistence service source
        /// </summary>
        public TObject Get<TObject>(Guid? key) where TObject : IdentifiedData, new()
		{
			var persistenceService = ApplicationContext.Current.GetService<IRepositoryService<TObject>>();
            if (persistenceService != null && key.HasValue)
                return persistenceService.Get(key.Value);
			return default(TObject);
		}

		/// <summary>
		/// Get the specified version
		/// </summary>
		public TObject Get<TObject>(Guid? key, Guid? versionKey) where TObject : IdentifiedData, IVersionedEntity, new()
		{
			var persistenceService = ApplicationContext.Current.GetService<IRepositoryService<TObject>>();
            if (persistenceService != null && key.HasValue)
                return persistenceService.Find(o => o.Key == key).FirstOrDefault();
            else if(persistenceService != null && key.HasValue && versionKey.HasValue)
                return persistenceService.Find(o => o.Key == key && o.VersionKey == versionKey).FirstOrDefault ();
			return default(TObject);
		}

        /// <summary>
        /// Get versioned relationships for the object
        /// </summary>
        public IEnumerable<TObject> GetRelations<TObject>(Guid? sourceKey, decimal? sourceVersionSequence) where TObject : IdentifiedData, IVersionedAssociation, new()
        {
            return this.Query<TObject>(o => o.SourceEntityKey == sourceKey).ToList();
        }

        /// <summary>
        /// Get versioned relationships for the object
        /// </summary>
        public IEnumerable<TObject> GetRelations<TObject>(Guid? sourceKey) where TObject : IdentifiedData, ISimpleAssociation, new()
        {
            return this.Query<TObject>(o => o.SourceEntityKey == sourceKey).ToList();
        }

        /// <summary>
        /// Query the specified object
        /// </summary>
        public IEnumerable<TObject> Query<TObject>(Expression<Func<TObject, bool>> query) where TObject : IdentifiedData, new()
		{
			var persistenceService = ApplicationContext.Current.GetService<IRepositoryService<TObject>>();
            if (persistenceService != null)
            {
                var tr = 0;
                return persistenceService.Find(query, 0, null, out tr);

            }
            return new List<TObject>();
		}

        #endregion

    }
}

