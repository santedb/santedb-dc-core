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
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Services;
using System;

namespace SanteDB.DisconnectedClient.Services.Local
{
    /// <summary>
    /// Local patient repository service
    /// </summary>
    public class LocalPatientRepository : GenericLocalNullifiedRepository<Patient>, IRepositoryService<Patient>
    {


        /// <summary>
        /// Merges two patients together
        /// </summary>
        /// <param name="survivor">The surviving patient record</param>
        /// <param name="victim">The victim patient record</param>
        /// <returns>A new version of patient <paramref name="a" /> representing the merge</returns>
        /// <exception cref="System.InvalidOperationException">If the persistence service is not found.</exception>
        /// <exception cref="System.NotImplementedException"></exception>
        public Patient Merge(Patient survivor, Patient victim)
        {
            // TODO: Do this
            throw new NotImplementedException();
        }

        /// <summary>
        /// Un-merge two patients
        /// </summary>
        public Patient UnMerge(Patient patient, Guid versionKey)
        {
            throw new NotImplementedException();
        }

    }
}