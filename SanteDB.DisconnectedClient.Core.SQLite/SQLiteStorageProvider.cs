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
 * Date: 2018-8-25
 */
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using SanteDB.DisconnectedClient.Core.Data;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Mail;
using SanteDB.DisconnectedClient.SQLite.Security;
using SanteDB.DisconnectedClient.SQLite.Synchronization;
using SanteDB.DisconnectedClient.SQLite.Warehouse;
using System;
using System.Collections.Generic;
using System.IO;

namespace SanteDB.DisconnectedClient.SQLite
{
    /// <summary>
    /// A storage provider for SQLite
    /// </summary>
    public class SQLiteStorageProvider : IDataProvider
    {
        /// <summary>
        /// Get the invariant name
        /// </summary>
        public string Invariant => "sqlite";

        /// <summary>
        /// Get the name of the storage proivider
        /// </summary>
        public string Name => "SQLite";

        /// <summary>
        /// Get the operating system ID on which this is supported
        /// </summary>
        public OperatingSystemID Platform => OperatingSystemID.Android | OperatingSystemID.MacOS | OperatingSystemID.Win32 | OperatingSystemID.Linux;

        /// <summary>
        /// Client only
        /// </summary>
        public SanteDBHostType HostType => SanteDBHostType.Client;

        /// <summary>
        /// Configuration options
        /// </summary>
        public Dictionary<String, ConfigurationOptionType> Options
        {
            get
            {
                var retVal = new Dictionary<string, ConfigurationOptionType>();
                if (ApplicationContext.Current.GetCurrentContextSecurityKey() != null)
                    retVal.Add("encrypt", ConfigurationOptionType.Boolean);
                return retVal;
            }
        }

        /// <summary>
        /// Progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// Configure
        /// </summary>
        public bool Configure(SanteDBConfiguration configuration, Dictionary<String, Object> options)
        {

            string dataDirectory = options["DataDirectory"].ToString();

            // Connection Strings
            DcDataConfigurationSection dataSection = new DcDataConfigurationSection()
            {
                MainDataSourceConnectionStringName = "santeDbData",
                MessageQueueConnectionStringName = "santeDbQueue",
                MailDataStore = "santeDbMail",
                ConnectionString = new System.Collections.Generic.List<ConnectionString>() {
                    new ConnectionString () {
                        Name = "santeDbData",
                        Value = $"dbfile={Path.Combine(dataDirectory, "SanteDB.sqlite")};encrypt={options["encrypt"].ToString().ToLower()}",
                        Provider = "sqlite"
                    },
                    new ConnectionString () {
                        Name = "santeDbMail",
                        Value = $"dbfile={Path.Combine(dataDirectory, "SanteDB.mail.sqlite")};encrypt={options["encrypt"].ToString().ToLower()}",
                        Provider = "sqlite"
                    },
                    new ConnectionString () {
                        Name = "santeDbSearch",
                        Value = $"dbfile={Path.Combine(dataDirectory, "SanteDB.ftsearch.sqlite")};encrypt={options["encrypt"].ToString().ToLower()}",
                        Provider = "sqlite"
                    },
                    new ConnectionString () {
                        Name = "santeDbQueue",
                        Value = $"dbfile={Path.Combine(dataDirectory, "SanteDB.queue.sqlite")};encrypt={options["encrypt"].ToString().ToLower()}",
                        Provider = "sqlite"
                    },
                    new ConnectionString () {
                        Name = "santeDbWarehouse",
                        Value = $"dbfile={Path.Combine(dataDirectory, "SanteDB.warehouse.sqlite")};encrypt={options["encrypt"].ToString().ToLower()}",
                        Provider = "sqlite"
                    },
                    new ConnectionString () {
                        Name = "santeDbAudit",
                        Value = $"dbfile={Path.Combine(dataDirectory, "SanteDB.audit.sqlite")};encrypt={options["encrypt"].ToString().ToLower()}",
                        Provider = "sqlite"
                    }
                }
            };
            configuration.Sections.RemoveAll(o => o is DataConfigurationSection);
            configuration.Sections.Add(dataSection);
            // Services
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Insert(0, typeof(SQLiteConnectionManager).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLitePersistenceService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteMailPersistenceService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteQueueManagerService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteSynchronizationLog).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteDatawarehouse).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteReportDatasource).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteRoleProviderService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteIdentityService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLitePolicyInformationService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteAuditRepositoryService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteDeviceIdentityProviderService).AssemblyQualifiedName);
            
            // SQLite provider
#if NOCRYPT
			appSection.ServiceTypes.Add(typeof(SQLite.Net.Platform.Generic.SQLitePlatformGeneric).AssemblyQualifiedName);
#else
            switch (ApplicationContext.Current.OperatingSystem)
            {
                case OperatingSystemID.Win32:
                    if (options.ContainsKey("encrypt") && options["encrypt"].Equals(true))
                        configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add("SQLite.Net.Platform.SqlCipher.SQLitePlatformSqlCipher, SQLite.Net.Platform.SqlCipher");
                    else
                        configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add("SQLite.Net.Platform.Generic.SQLitePlatformGeneric, SQLite.Net.Platform.Generic");
                    break;
                case OperatingSystemID.MacOS:
                case OperatingSystemID.Linux:
                    configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add("SQLite.Net.Platform.Generic.SQLitePlatformGeneric, SQLite.Net.Platform.Generic");
                    break;
                case OperatingSystemID.Android:
                    configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add("SQLite.Net.Platform.XamarinAndroid.SQLitePlatformAndroid, SQLite.Net.Platform.XamarinAndroid");
                    break;
            }
#endif

            return true;
        }

        /// <summary>
        /// Deploy the identified feature
        /// </summary>
        public bool Deploy(IDataFeature feature, string connectionStringName, SanteDBConfiguration configuration)
        {
            throw new NotSupportedException("This provider cannot be used in the Server Environment");
        }

        /// <summary>
        /// Get databases
        /// </summary>
        public IEnumerable<string> GetDatabases(string connectionStringName)
        {
            return new String[] { "$$main$$" };
        }

        /// <summary>
        /// Get data features
        /// </summary>
        public IEnumerable<IDataFeature> GetDataFeatures()
        {
            return new List<IDataFeature>();
        }
    }
}
