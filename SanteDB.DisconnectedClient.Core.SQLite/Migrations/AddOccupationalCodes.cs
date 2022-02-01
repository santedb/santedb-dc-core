/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Roles;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Add occupational codes
    /// </summary>
    public class AddOccupationalCodes : IDataMigration
    {

        private Tracer m_tracer = Tracer.GetTracer(typeof(AddOccupationalCodes));

        /// <summary>
        /// Description of the migration
        /// </summary>
        public string Description => "Adds missing fields to patient and person";

        /// <summary>
        /// Identifier
        /// </summary>
        public string Id => "999-patient-occ-code";

        /// <summary>
        /// Install the objects
        /// </summary>
        public bool Install()
        {
            try
            {

                var connStr = ApplicationContext.Current.ConfigurationManager.GetConnectionString("santeDbData");

                // Get a connection to the search database
                var conn = SQLiteConnectionManager.Current.GetReadWriteConnection(connStr);
                using (conn.Lock())
                {
                    conn.MigrateTable<DbPatient>();
                    conn.MigrateTable<DbPerson>();
                } // release lock

                return true;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error installing challenge tables {0}", e);
                throw;
            }
        }
    }
}
