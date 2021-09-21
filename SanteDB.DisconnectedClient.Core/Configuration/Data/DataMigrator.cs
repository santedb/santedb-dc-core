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
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.i18n;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Configuration.Data
{
    /// <summary>
    /// Represents a data migrator which is responsible for performing data migrations
    /// </summary>
    public class ConfigurationMigrator
    {
        // Migrations
        private readonly List<IConfigurationMigration> m_migrations;

        // Tracer
        private readonly Tracer m_tracer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Configuration.Data.DataMigrator"/> class.
        /// </summary>
        /// <param name="configuration">Configuration.</param>
        public ConfigurationMigrator()
        {
            try
            {
                this.m_tracer = Tracer.GetTracer(this.GetType());
                this.m_migrations = new List<IConfigurationMigration>();

                this.m_tracer.TraceInfo("Scanning for data migrations...");

                // Scan for migrations 
                var migrations = ApplicationContext.Current.Configuration.GetSection<ApplicationServiceContextConfigurationSection>()?.ServiceProviders
                    ?.Select(o => o?.Type?.GetTypeInfo().Assembly).OfType<Assembly>().Distinct().SelectMany(a => a?.DefinedTypes).ToArray();
                if (migrations != null)
                {
                    foreach (var dbm in migrations)
                    {
                        try
                        {
                            if (dbm.AsType() == typeof(ConfigurationMigrator) ||
                                !typeof(IConfigurationMigration).GetTypeInfo().IsAssignableFrom(dbm))
                            {
                                continue;
                            }

                            var migration = Activator.CreateInstance(dbm.AsType()) as IConfigurationMigration;
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
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Won't load migrations: {0}", e);
            }
        }

        /// <summary>
        /// Assert that all data migrations have occurred
        /// </summary>
        public void Ensure(bool includeDataMigrations)
        {

            this.m_tracer.TraceInfo("Ensuring database is up to date");
            // Migration order
            foreach (var m in this.GetProposal(includeDataMigrations))
            {
                try
                {
                    ApplicationContext.Current.SetProgress(Strings.locale_setting_migration, 0);
                    this.m_tracer.TraceVerbose("Will Install {0}", m.Id);
                    if (!m.Install())
                    {
                        throw new ConfigurationMigrationException(m);
                    }

                    ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MigrationLog.Entry.Add(new DataMigrationLog.DataMigrationEntry(m));
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error running migration {0} - {1}", m.Id, e);
                    throw new ConfigurationMigrationException(m, e);
                }
            }

        }

        /// <summary>
        /// Get the list of data migrations that need to occur for the application to be in the most recent state
        /// </summary>
        /// <returns>The proposal.</returns>
        public List<IConfigurationMigration> GetProposal(bool includeDataMigrations)
        {
            var retVal = new List<IConfigurationMigration>();

            this.m_tracer.TraceInfo("Generating data migration proposal...");
            foreach (var itm in this.m_migrations.OrderBy(o => o.Id))
            {
                if (itm is IDataMigration && !includeDataMigrations)
                {
                    continue;
                }

                var migrationLog = ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MigrationLog.Entry.Find(o => o.Id == itm.Id);
                this.m_tracer.TraceVerbose("Migration {0} ... {1}", itm.Id, migrationLog == null ? "Install" : "Skip - Installed on " + migrationLog.Date);
                if (migrationLog == null)
                {
                    retVal.Add(itm);
                }
            }
            return retVal;
        }
    }
}

