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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Configuration.Data.Migrations
{
    /// <summary>
    /// Update for migrating Edmonton CTP5 to f4
    /// </summary>
    public class CatalogEdmonton4Update : IDbMigration
    {
        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description
        {
            get
            {
                return "Updates Edmonton CTP4 to CTP5 database";
            }
        }

        /// <summary>
        /// Gets the identifier of the migration
        /// </summary>
        public string Id
        {
            get
            {
                return "santedb-edmonton-ctp3-CTP5";
            }
        }

        /// <summary>
        /// Install the migration
        /// </summary>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());

            // Database for the SQL Lite connection
            var db = SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
            using (db.Lock())
            {
                try
                {
                    db.Execute("ALTER TABLE act_participation ADD sequence NUMERIC(10,0)");
                }
                catch { }
                try
                {

                    // Now we query and set the sequencing
                    int seq = 0;
                    foreach (var itm in db.Table<DbActParticipation>())
                    {
                        itm.Sequence = seq++;
                        db.Update(itm);
                    }
                    return true;
                }
                catch (Exception e)
                {
                    tracer.TraceError("Error installing CTP5 update: {0}", e);
                    return true;
                    //throw new InvalidOperationException("Could not install Edmonton CTP5 update");
                }
            }
        }
    }
}
