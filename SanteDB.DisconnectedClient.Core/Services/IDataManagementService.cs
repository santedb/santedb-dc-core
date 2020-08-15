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
using SanteDB.Core.Services;

namespace SanteDB.DisconnectedClient.Services
{
    /// <summary>
    /// Represents a data connection manager
    /// </summary>
    public interface IDataManagementService
    {
        /// <summary>
        /// Instructs the data connection manager to compact data
        /// </summary>
        void Compact();

        /// <summary>
        /// Copy the database to another location for backup purposes
        /// </summary>
        /// <param name="passkey">The passkey to use to encrypt the backup</param>
        /// <returns>The location where backup can be found</returns>
        string Backup(string passkey);

        /// <summary>
        /// Rekey all databases
        /// </summary>
        void RekeyDatabases();
    }
}
