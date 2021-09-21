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
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SQLite.Net;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Add the hidden attribute to acts and entities
    /// </summary>
    public class HiddenAttributeAdd : IDataMigration
    {
        /// <summary>
        /// Get the database migration
        /// </summary>
        public string Id => "santedb-Langley-hidden-attribute";

        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description => "Adds the hidden attribute";

        /// <summary>
        /// Install the migration
        /// </summary>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());

            // Database for the SQL Lite connection
            var db = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
            using (db.Lock())
            {
                try
                {
                    db.Execute("ALTER TABLE act ADD hidden BOOLEAN NOT NULL default 0;");
                    db.Execute("UPDATE act SET hidden = 1, obsoletionTime = null, obsoletedBy = null WHERE obsoletedBy = X'76A0DCFA90366E4AAF9EF1CD68E8C7E8';");
                    db.Execute("ALTER TABLE entity ADD hidden BOOLEAN NOT NULL default 0;");
                    db.Execute("UPDATE entity SET hidden = 1, obsoletionTime = null, obsoletedBy = null WHERE obsoletedBy = X'76A0DCFA90366E4AAF9EF1CD68E8C7E8';");
                    return true;
                }
                catch (SQLiteException e) when (e.Message == "duplicate column name: hidden")
                {
                    tracer.TraceWarning("Could not alter tables for update - {0}", e);
                    return true;
                }
                catch (Exception e)
                {
                    tracer.TraceError("Could not alter tables for update - {0}", e);
                    throw new Exception("Could not apply database updates", e);
                }
            }
        }
    }
}
