using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Data;
using SanteDB.DisconnectedClient.SQLite;
using SanteDB.DisconnectedClient.SQLite.Alerting;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Security;
using SanteDB.DisconnectedClient.SQLite.Synchronization;
using SanteDB.DisconnectedClient.SQLite.Warehouse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Data
{
    /// <summary>
    /// A storage provider for SQLite
    /// </summary>
    public class SQLiteStorageProvider : IStorageProvider
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
        /// Configuration options
        /// </summary>
        public Dictionary<String, ConfigurationOptionType> Options => new Dictionary<string, ConfigurationOptionType>() { { "encrypt", ConfigurationOptionType.Boolean } };

        /// <summary>
        /// Configure
        /// </summary>
        public bool Configure(SanteDBConfiguration configuration, Dictionary<String, Object> options)
        {
            
            // Connection Strings
            DataConfigurationSection dataSection = new DataConfigurationSection()
            {
                MainDataSourceConnectionStringName = "openIzData",
                MessageQueueConnectionStringName = "openIzQueue",
                ConnectionString = new System.Collections.Generic.List<ConnectionString>() {
                    new ConnectionString () {
                        Name = "openIzData",
                        Value = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), ApplicationContext.Current.Application.Name, "SanteDB.sqlite")
                    },
                    new ConnectionString () {
                        Name = "openIzSearch",
                        Value = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), ApplicationContext.Current.Application.Name,"SanteDB.ftsearch.sqlite")
                    },
                    new ConnectionString () {
                        Name = "openIzQueue",
                        Value = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), ApplicationContext.Current.Application.Name,"SanteDB.MessageQueue.sqlite")
                    },
                    new ConnectionString () {
                        Name = "openIzWarehouse",
                        Value = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), ApplicationContext.Current.Application.Name,"SanteDB.warehouse.sqlite")
                    },
                    new ConnectionString () {
                        Name = "openIzAudit",
                        Value = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), ApplicationContext.Current.Application.Name, "SanteDB.audit.sqlite")
                    }
                }
            };
            configuration.Sections.RemoveAll(o => o is DataConfigurationSection);
            configuration.Sections.Add(dataSection);
            // Services
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Insert(0, typeof(SQLiteConnectionManager).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLitePersistenceService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteAlertPersistenceService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteQueueManagerService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteSynchronizationLog).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteDatawarehouse).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteReportDatasource).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteRoleProviderService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLiteIdentityService).AssemblyQualifiedName);
            configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(SQLitePolicyInformationService).AssemblyQualifiedName);

            // SQLite provider
            #if NOCRYPT
			appSection.ServiceTypes.Add(typeof(SQLite.Net.Platform.Generic.SQLitePlatformGeneric).AssemblyQualifiedName);
            #else
            switch(ApplicationContext.Current.OperatingSystem)
            {
                case OperatingSystemID.Win32:
                    if (options["encrypt"].Equals(true))
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
    }
}
