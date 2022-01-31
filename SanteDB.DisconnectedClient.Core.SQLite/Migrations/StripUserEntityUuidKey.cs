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
using System;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Data migration for security user key
    /// </summary>
    public class StripUserEntityUuidKey : IDataMigration
    {
        /// <summary>
        /// Get the description
        /// </summary>
        public string Description => "Strip required from the user entity table";

        /// <summary>
        /// Gets the id
        /// </summary>
        public string Id => "zzz-strip-dbuserentity-notnull";

        /// <summary>
        /// Install the extension
        /// </summary>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());

            try
            {

                // Database for the SQL Lite connection
                var db = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
                using (db.Lock())
                {
                    db.MigrateTable<DbUserEntity>();
                }
            }
            catch (Exception)
            {
                tracer.TraceWarning("Could not apply migration to strip required from user entity uuid");
            }
            return true;
        }
    }
}
