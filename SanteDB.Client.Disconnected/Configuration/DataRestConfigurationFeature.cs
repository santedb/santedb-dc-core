﻿using Newtonsoft.Json.Linq;
using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Configuration;
using SanteDB.Persistence.Auditing.ADO.Configuration;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Services;
using SanteDB.Persistence.PubSub.ADO.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SanteDB.Client.Disconnected.Configuration
{
    /// <summary>
    /// Represents a configuration feautre that can handle the "data"
    /// </summary>
    public class DataRestConfigurationFeature : IClientConfigurationFeature
    {

        public const string GLOBAL_DATA_PROVIDER_SETTING = "provider";
        public const string GLOBAL_CONNECTION_STRING_SETTING = "options";
        public const string CONNECTION_PER_FEATURE_SETTING = "connections";

        private readonly DataConfigurationSection m_connectStringSection;
        private readonly DataRetentionConfigurationSection m_retentionConfigurationSection;
        private readonly OrmConfigurationSection m_ormConfigurationSection;
        private readonly IEnumerable<OrmConfigurationBase> m_dataConfigurationSections;

        /// <summary>
        /// DI constructor
        /// </summary>
        public DataRestConfigurationFeature(IConfigurationManager configurationManager)
        {
            this.m_connectStringSection = configurationManager.GetSection<DataConfigurationSection>();
            this.m_retentionConfigurationSection = configurationManager.GetSection<DataRetentionConfigurationSection>();
            this.m_ormConfigurationSection = configurationManager.GetSection<OrmConfigurationSection>();
            this.m_dataConfigurationSections = configurationManager.Configuration.Sections.OfType<OrmConfigurationBase>().ToArray();
        }

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public string Name => "data";

        /// <inheritdoc/>
        public ConfigurationDictionary<string, object> Configuration => this.GetConfiguration();

        private ConfigurationDictionary<string, object> GetConfiguration()
        {
            var retVal = new ConfigurationDictionary<string, object>();

            retVal[CONNECTION_PER_FEATURE_SETTING] = this.m_dataConfigurationSections.ToDictionary(o => o.GetType().Name, o => this.ParseDataConfigurationSection(o));

            // Is there only one configuration section?
            if (this.m_connectStringSection.ConnectionString.Count == 1)
            {
                var ctOI = this.m_connectStringSection.ConnectionString.First();
                retVal[GLOBAL_DATA_PROVIDER_SETTING] = ctOI.Provider;
                retVal[GLOBAL_CONNECTION_STRING_SETTING] = this.ParseConnectionString(ctOI);
            }

            return retVal;
        }

        /// <summary>
        /// Parse data configuration section
        /// </summary>
        private IDictionary<String, Object> ParseDataConfigurationSection(OrmConfigurationBase ormSection)
        {
            var retVal = new ConfigurationDictionary<String, Object>();
            retVal[GLOBAL_DATA_PROVIDER_SETTING] = ormSection.Provider;
            var connectionString = this.m_connectStringSection.ConnectionString.Find(o => o.Name == ormSection.ReadWriteConnectionString);
            retVal[GLOBAL_CONNECTION_STRING_SETTING] = this.ParseConnectionString(connectionString);
            return retVal;
        }

        /// <summary>
        /// Parse a connection string
        /// </summary>
        private IDictionary<String, Object> ParseConnectionString(ConnectionString connectionString)
        {
            if (connectionString == null)
            {
                return new ConfigurationDictionary<String, Object>();
            }
            else
            {
                var provider = DataConfigurationSection.GetDataConfigurationProvider(connectionString.Provider);
                return provider.ParseConnectionString(connectionString);
            }
        }

        /// <inheritdoc/>
        public string ReadPolicy => PermissionPolicyIdentifiers.AlterSystemConfiguration;

        /// <inheritdoc/>
        public string WritePolicy => PermissionPolicyIdentifiers.AlterSystemConfiguration;

        /// <inheritdoc/>
        public bool Configure(SanteDBConfiguration configuration, IDictionary<string, object> featureConfiguration)
        {
            var dataSection = configuration.GetSection<DataConfigurationSection>();
            if(dataSection == null)
            {
                dataSection = new DataConfigurationSection()
                {
                    ConnectionString = new List<ConnectionString>()
                };
                configuration.AddSection(dataSection);
            }
            var ormSection = configuration.GetSection<OrmConfigurationSection>();
            if(ormSection == null)
            {
                ormSection = new OrmConfigurationSection()
                {
                    AdoProvider = AppDomain.CurrentDomain.GetAllTypes().Where(t => typeof(DbProviderFactory).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).Select(t => new ProviderRegistrationConfiguration(t.Namespace.StartsWith("System") ? t.Name : t.Namespace.Split('.')[0], t)).ToList(),
                    Providers = DataConfigurationSection.GetDataConfigurationProviders().Where(o => o.HostType == ApplicationServiceContext.Current.HostType).Select(o => new ProviderRegistrationConfiguration(o.Invariant, o.DbProviderType)).ToList()
                };
            }

            // Create and update the configuration feature settings
            dataSection.ConnectionString.Clear();
            if (featureConfiguration.TryGetValue(CONNECTION_PER_FEATURE_SETTING, out var itmSettingCollectionRaw) && itmSettingCollectionRaw is JObject itmSettingCollection)
            {
                foreach (var itm in this.m_dataConfigurationSections)
                {
                    if (itmSettingCollection.TryGetValue(itm.GetType().Name, out var itmSettingRaw) && itmSettingRaw is JObject itmSetting)
                    {
                        if (!itmSetting.TryGetValue(GLOBAL_DATA_PROVIDER_SETTING, out var providerRaw))
                        {
                            throw new ArgumentNullException(GLOBAL_DATA_PROVIDER_SETTING);
                        }
                        var provider = DataConfigurationSection.GetDataConfigurationProvider(providerRaw.ToString());
                        if (provider == null)
                        {
                            throw new InvalidOperationException(String.Format(ErrorMessages.SERVICE_NOT_FOUND, providerRaw));
                        }

                        // Create a connection string
                        if (itmSetting.TryGetValue(GLOBAL_CONNECTION_STRING_SETTING, out var connectionStringRaw) && connectionStringRaw is IDictionary<String, JToken> connectionString)
                        {
                            var cstr = provider.CreateConnectionString(connectionString.ToDictionary(o => o.Key, o => (object)o.Value));
                            cstr.Name = itm.GetType().Name;
                            cstr.Provider = itm.ProviderType = provider.Invariant;
                            dataSection.ConnectionString.Add(cstr);
                            itm.ReadWriteConnectionString = itm.ReadonlyConnectionString = cstr.Name;

                        }
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.DATA_STRUCTURE_NOT_APPROPRIATE, itm.GetType().Name, "invalid dictionary"));
                    }
                }
            }
            else if(featureConfiguration.TryGetValue(GLOBAL_DATA_PROVIDER_SETTING, out var providerRaw) &&
                featureConfiguration.TryGetValue(GLOBAL_CONNECTION_STRING_SETTING, out var connectionStringDataRaw) &&
                connectionStringDataRaw is JObject connectionStringData) // There is a global configuration?
            {
                var provider = DataConfigurationSection.GetDataConfigurationProvider(providerRaw.ToString());
                if (provider == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.SERVICE_NOT_FOUND, providerRaw));
                }

                // Create a connection string
                if (featureConfiguration.TryGetValue(GLOBAL_CONNECTION_STRING_SETTING, out var connectionStringRaw) && connectionStringRaw is IDictionary<String, JToken> connectionString)
                {
                    var cstr = provider.CreateConnectionString(connectionString.ToDictionary(o => o.Key, o => (object)o.Value));
                    cstr.Name = "main";
                    dataSection.ConnectionString.Add(cstr);
                    foreach(var itm in this.m_dataConfigurationSections)
                    {
                        itm.ReadonlyConnectionString = itm.ReadWriteConnectionString = "main";
                    }
                }

            }
            else
            {
                return true; // Skip
            }

            // Register providers
            var appSetting = configuration.GetSection<ApplicationServiceContextConfigurationSection>();
            appSetting.ServiceProviders.RemoveAll(o => o.Type.Implements(typeof(IDataPersistenceService)));
            appSetting.ServiceProviders.RemoveAll(o => o.Type.Implements(typeof(IDataPersistenceService)));

            // Add any services which use any of the ORM configuration sections - this is done so that plugins can define their own OrmConfigurationSections and this 
            // tool will add the appropriate services
            foreach (var itm in this.m_dataConfigurationSections)
            {
                var servicesUsingConfiguration = AppDomain.CurrentDomain.GetAllTypes()
                    .Where(o => o.Implements(typeof(IServiceImplementation)) && !o.IsAbstract && !o.IsInterface && o.GetCustomAttribute<ServiceProviderAttribute>()?.Configuration == itm.GetType())
                    .Select(o => new TypeReferenceConfiguration(o));
                appSetting.ServiceProviders.AddRange(servicesUsingConfiguration);
            }

            return true;
        }
    }
}
