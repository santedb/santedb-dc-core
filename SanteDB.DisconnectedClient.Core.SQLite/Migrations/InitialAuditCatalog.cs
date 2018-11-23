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
 * User: justin
 * Date: 2018-6-28
 */
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Security.Audit.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using SanteDB.DisconnectedClient.Core;

namespace SanteDB.DisconnectedClient.SQLite.Configuration.Data.Migrations
{
    /// <summary>
    /// Database migration for audit
    /// </summary>
    public class InitialAuditCatalog : IDbMigration
    {
        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description
        {
            get
            {
                return "Audit Catalog";
            }
        }

        /// <summary>
        /// Get the identifier of the migration
        /// </summary>
        public string Id
        {
            get
            {
                return "000-init-santedb-dalhouse-audit";
            }
        }

        /// <summary>
        /// Install the migration
        /// </summary>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());
            // Database for the SQL Lite connection
            var connStr = ApplicationContext.Current?.Configuration.GetConnectionString("santeDbAudit")?.Value;
            if (String.IsNullOrEmpty(connStr))
                return true;
            var db = SQLiteConnectionManager.Current.GetConnection(connStr);
            using (db.Lock())
            {
                try
                {

                    db.CreateTable<DbAuditData>();
                    db.CreateTable<DbAuditActor>();
                    db.CreateTable<DbAuditActorAssociation>();
                    db.CreateTable<DbAuditCode>();
                    db.CreateTable<DbAuditObject>();
                    return true;
                }
                catch(Exception e)
                {
                    tracer.TraceError("Error deploying Audit repository: {0}", e);
                    return false;
                }
            }
        }
    }
}