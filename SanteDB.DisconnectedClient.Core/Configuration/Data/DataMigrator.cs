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
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.i18n;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Core.Configuration.Data
{
    /// <summary>
    /// Represents a data migrator which is responsible for performing data migrations
    /// </summary>
    public class DataMigrator
    {

        // Tracer
        private Tracer m_tracer;

        // Migrations
        private List<IDbMigration> m_migrations;

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Core.Configuration.Data.DataMigrator"/> class.
        /// </summary>
        /// <param name="configuration">Configuration.</param>
        public DataMigrator()
        {
            try
            {
                this.m_tracer = Tracer.GetTracer(this.GetType());
                this.m_migrations = new List<IDbMigration>();

                this.m_tracer.TraceInfo("Scanning for data migrations...");

                // Scan for migrations 
                var migrations = ApplicationContext.Current.Configuration.GetSection<ApplicationServiceContextConfigurationSection>()?.ServiceProviders
                    ?.Select(o => o?.Type?.GetTypeInfo().Assembly).Distinct().SelectMany(a => a?.DefinedTypes);
                if (migrations != null)
                    foreach (var dbm in migrations)
                    {
                        try
                        {
                            if (dbm.AsType() == typeof(DataMigrator) ||
                                !typeof(IDbMigration).GetTypeInfo().IsAssignableFrom(dbm))
                                continue;

                            IDbMigration migration = Activator.CreateInstance(dbm.AsType()) as IDbMigration;
                            if (migration != null)
                            {
                                this.m_tracer.TraceVerbose("Found data migrator {0}...", migration.Id);
                                this.m_migrations.Add(migration);
                            }
                        }
                        catch
                        {
                        }
                    }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Won't load migrations: {0}", e);
            }
        }

        /// <summary>
        /// Assert that all data migrations have occurred
        /// </summary>
        public void Ensure()
        {

            this.m_tracer.TraceInfo("Ensuring database is up to date");
            // Migration order
            foreach (var m in this.GetProposal())
            {
                ApplicationContext.Current.SetProgress(Strings.locale_setting_migration, 0);
                this.m_tracer.TraceVerbose("Will Install {0}", m.Id);
                if (!m.Install())
                    throw new DataMigrationException(m);
                else
                    ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MigrationLog.Entry.Add(new DataMigrationLog.DataMigrationEntry(m));
            }

        }

        /// <summary>
        /// Get the list of data migrations that need to occur for the application to be in the most recent state
        /// </summary>
        /// <returns>The proposal.</returns>
        public List<IDbMigration> GetProposal()
        {
            List<IDbMigration> retVal = new List<IDbMigration>();

            this.m_tracer.TraceInfo("Generating data migration proposal...");
            foreach (var itm in this.m_migrations.OrderBy(o => o.Id))
            {
                var migrationLog = ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MigrationLog.Entry.Find(o => o.Id == itm.Id);
                this.m_tracer.TraceVerbose("Migration {0} ... {1}", itm.Id, migrationLog == null ? "Install" : "Skip - Installed on " + migrationLog.Date.ToString());
                if (migrationLog == null)
                    retVal.Add(itm);
            }
            return retVal;
        }
    }
}

