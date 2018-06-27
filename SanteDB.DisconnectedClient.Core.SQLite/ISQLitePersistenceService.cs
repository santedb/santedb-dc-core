/*
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
using SanteDB.Core.Services;
using System;
using System.Collections;
using System.Security.Principal;
using SanteDB.Core.Model.Interfaces;

namespace SanteDB.DisconnectedClient.SQLite
{
    /// <summary>
    /// Represents an ADO based IDataPersistenceServie
    /// </summary>
    public interface ISQLitePersistenceService : IDataPersistenceService
    {
        /// <summary>
        /// Inserts the specified object
        /// </summary>
        Object Insert(SQLiteDataContext context, Object data);

        /// <summary>
        /// Updates the specified data
        /// </summary>
        Object Update(SQLiteDataContext context, Object data);

        /// <summary>
        /// Obsoletes the specified data
        /// </summary>
        Object Obsolete(SQLiteDataContext context, Object data);

        /// <summary>
        /// Gets the specified data
        /// </summary>
        Object Get(SQLiteDataContext context, Guid id);

        /// <summary>
        /// Map to model instance
        /// </summary>
        Object ToModelInstance(object domainInstance, SQLiteDataContext context);
    }

    /// <summary>
    /// ADO associative persistence service
    /// </summary>
    public interface ISQLiteAssociativePersistenceService : ISQLitePersistenceService
    {
        /// <summary>
        /// Get the set objects from the source
        /// </summary>
        IEnumerable GetFromSource(SQLiteDataContext context, Guid id, decimal? versionSequenceId);
    }
}