/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Mail;
using SanteDB.DisconnectedClient.SQLite.Model;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Represents an initial alert catalog
    /// </summary>
    public class InitialMessageCatalog : IDbMigration
    {

        /// <summary>
        /// Gets the id of this catalog
        /// </summary>
        public string Id => "000-init-mail";

        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description => "Mail message catalog";

        /// <summary>
        /// Installation
        /// </summary>
        public bool Install()
        {
            // Database for the SQL Lite connection
            var tracer = Tracer.GetTracer(typeof(InitialMessageCatalog));

            var db = SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MailDataStore));
            try
            {
                using (db.Lock())
                {

                    db.BeginTransaction();

                    // Migration log create and check
                    db.CreateTable<DbMigrationLog>();
                    if (db.Table<DbMigrationLog>().Count(o => o.MigrationId == this.Id) > 0)
                    {
                        tracer.TraceWarning("Migration 000-init-alerts already installed");
                        return true;
                    }
                    else
                        tracer.TraceInfo("Installing initial alerts catalog");
                    db.Insert(new DbMigrationLog()
                    {
                        MigrationId = this.Id,
                        InstallationDate = DateTime.Now
                    });

                    db.CreateTable<DbMailMessage>();

                    db.Commit();

                    return true;
                }
            }
            catch (Exception e)
            {
                db.Rollback();
                tracer.TraceError("Error installing initial alerts catalog: {0}", e);
                throw;
            }

        }
    }
}
