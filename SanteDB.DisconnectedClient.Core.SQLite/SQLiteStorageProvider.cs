/*
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
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Security.Audit;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Mail;
using SanteDB.DisconnectedClient.SQLite.Mdm;
using SanteDB.DisconnectedClient.SQLite.Security;
using SanteDB.DisconnectedClient.SQLite.Synchronization;
using SanteDB.DisconnectedClient.SQLite.Warehouse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite
{
    /// <summary>
    /// A storage provider for SQLite
    /// </summary>
    public class SQLiteStorageProvider : IDataConfigurationProvider
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
        /// No option groups
        /// </summary>
        public Dictionary<string, string[]> OptionGroups => null;

        /// <summary>
        /// Get the provider type
        /// </summary>
        public Type DbProviderType => Type.GetType("SanteDB.OrmLite.Providers.SqliteProvider, SanteDB.OrmLite, Version=1.0.0.0");

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
            if (!options.ContainsKey("encrypt"))
                options.Add("encrypt", false);
            
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
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Insert(0, new TypeReferenceConfiguration(typeof(SQLiteConnectionManager)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLitePersistenceService)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteMailPersistenceService)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteQueueManagerService)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteSynchronizationLog)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteBiDataSource)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteRoleProviderService)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteIdentityService)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLitePolicyInformationService)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteAuditRepositoryService)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteDeviceIdentityProviderService)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(MdmDataManager)));
            configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration(typeof(SQLiteSecurityChallengeService)));
            // SQLite provider
#if NOCRYPT
			appSection.ServiceTypes.Add(typeof(SQLite.Net.Platform.Generic.SQLitePlatformGeneric).AssemblyQualifiedName);
#else
            var osiService = ApplicationServiceContext.Current.GetService<IOperatingSystemInfoService>();
            switch (osiService.OperatingSystem)
            {
                case OperatingSystemID.Win32:
                    if (options.ContainsKey("encrypt") && options["encrypt"].Equals(true))
                        configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration("SQLite.Net.Platform.SqlCipher.SQLitePlatformSqlCipher, SQLite.Net.Platform.SqlCipher"));
                    else
                        configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration("SQLite.Net.Platform.Generic.SQLitePlatformGeneric, SQLite.Net.Platform.Generic"));
                    break;
                case OperatingSystemID.MacOS:
                case OperatingSystemID.Linux:
                    configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration("SQLite.Net.Platform.Generic.SQLitePlatformGeneric, SQLite.Net.Platform.Generic"));
                    break;
                case OperatingSystemID.Android:
                    configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Add(new TypeReferenceConfiguration("SQLite.Net.Platform.XamarinAndroid.SQLitePlatformAndroid, SQLite.Net.Platform.XamarinAndroid"));
                    break;
            }
#endif

            return true;
        }

        /// <summary>
        /// Create a connection string to be inserted into the connection string section
        /// </summary>
        public ConnectionString CreateConnectionString(Dictionary<string, object> options)
        {
            StringBuilder sb = new StringBuilder();

            Object dbFile = null;
            if (!options.TryGetValue("Data Source", out dbFile))
                options.TryGetValue("dbfile", out dbFile);
            if (dbFile != null)
                sb.AppendFormat("dbfile={0}", dbFile);
            if (options.ContainsKey("encrypt"))
                sb.AppendFormat(";encrypt={0}", options["encrypt"].ToString().ToLower());
            return new ConnectionString()
            {
                Provider = this.Invariant,
                Name = $"conn{Guid.NewGuid().ToString().Substring(0, 8)}",
                Value = sb.ToString()
            };
        }

        /// <summary>
        /// Create a database
        /// </summary>
        public ConnectionString CreateDatabase(ConnectionString connectionString, string databaseName, string databaseOwner)
        {
            throw new NotSupportedException("This provider cannot create databases");
        }


        /// <summary>
        /// Deploy the identified feature
        /// </summary>
        public bool Deploy(IDataFeature feature, string connectionStringName, SanteDBConfiguration configuration)
        {
            throw new NotSupportedException("This provider cannot be used in the Server Environment");
        }

        /// <summary>
        /// Get all databases
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public IEnumerable<string> GetDatabases(ConnectionString connectionString)
        {
            return new String[] { "$$main$$" };
        }

        /// <summary>
        /// Get the features available for the connection string
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public IEnumerable<IDataFeature> GetFeatures(ConnectionString connectionString)
        {
            return new List<IDataFeature>();
        }

        /// <summary>
        /// Parse the specified connection string to known components
        /// </summary>
        public Dictionary<string, object> ParseConnectionString(ConnectionString connectionString)
        {
            return new Dictionary<string, object>()
            {
                { "dbfile", connectionString.GetComponent("dbfile") },
                { "encrypt", Boolean.Parse(connectionString.GetComponent("encrypt")) }
            };
        }

        /// <summary>
        /// Test the specified connection string
        /// </summary>
        public bool TestConnectionString(ConnectionString connectionString)
        {
            try
            {
                using (var conn = SQLiteConnectionManager.Current.GetReadWriteConnection(connectionString))
                    return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
