/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2023-6-21
 */
using Newtonsoft.Json.Linq;
using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace SanteDB.Client.Disconnected.Configuration
{
    /// <summary>
    /// Represents a configuration feautre that can handle the "data"
    /// </summary>
    public class DataRestConfigurationFeature : IClientConfigurationFeature
    {
        /// <summary>
        /// Configuration cache
        /// </summary>
        private ConfigurationDictionary<String, Object> m_configuration = null;

        /// <summary>
        /// The name of the database provider setting
        /// </summary>
        public const string GLOBAL_DATA_PROVIDER_SETTING = "provider";
        /// <summary>
        /// The name of the options for the database setting
        /// </summary>
        public const string GLOBAL_CONNECTION_STRING_SETTING = "options";
        /// <summary>
        /// The name of the connection feature setting
        /// </summary>
        public const string CONNECTION_PER_FEATURE_SETTING = "connections";

        /// <summary>
        /// ALE fields
        /// </summary>
        public const string ALE_SETTING = "ale";
        public const string ALE_ENABLED_SETTING = "enabled";
        public const string ALE_FIELDS_SETTING = "fields";

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
        public int Order => 50;

        /// <inheritdoc/>
        public string Name => "data";

        /// <inheritdoc/>
        public ConfigurationDictionary<string, object> Configuration => this.GetConfiguration();

        private ConfigurationDictionary<string, object> GetConfiguration()
        {
            if (this.m_configuration == null)
            {
                this.m_configuration = new ConfigurationDictionary<string, object>();
                this.m_configuration[CONNECTION_PER_FEATURE_SETTING] = this.m_dataConfigurationSections.ToDictionary(o => o.GetType().Name, o => this.ParseDataConfigurationSection(o));
                // Is there only one configuration section?
                if (this.m_connectStringSection.ConnectionString.Count == 1)
                {
                    var ctOI = this.m_connectStringSection.ConnectionString.First();
                    this.m_configuration[GLOBAL_DATA_PROVIDER_SETTING] = ctOI.Provider;
                    this.m_configuration[GLOBAL_CONNECTION_STRING_SETTING] = this.ParseConnectionString(ctOI);
                }
            }
            return this.m_configuration;
        }

        /// <summary>
        /// Parse data configuration section
        /// </summary>
        private IDictionary<String, Object> ParseDataConfigurationSection(OrmConfigurationBase ormSection)
        {
            var retVal = new ConfigurationDictionary<String, Object>();
            retVal[GLOBAL_DATA_PROVIDER_SETTING] = ormSection.Provider.Invariant;
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
            if (configuration.GetSection<ApplicationServiceContextConfigurationSection>().AppSettings?.Any(p => p.Key == "integration-mode" && p.Value == "online") == true)
            {
                return true;
            }

            var dataSection = configuration.GetSection<DataConfigurationSection>();
            if (dataSection == null)
            {
                dataSection = new DataConfigurationSection()
                {
                    ConnectionString = new List<ConnectionString>()
                };
                configuration.AddSection(dataSection);
            }
            var ormSection = configuration.GetSection<OrmConfigurationSection>();
            if (ormSection == null)
            {
                ormSection = new OrmConfigurationSection()
                {
                    AdoProvider = AppDomain.CurrentDomain.GetAllTypes().Where(t => typeof(DbProviderFactory).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).Select(t => new ProviderRegistrationConfiguration(t.Namespace.StartsWith("System") ? t.Name : t.Namespace.Split('.')[0], t)).ToList(),
                    Providers = DataConfigurationSection.GetDataConfigurationProviders().Where(o => o.HostType == ApplicationServiceContext.Current.HostType).Select(o => new ProviderRegistrationConfiguration(o.Invariant, o.DbProviderType)).ToList()
                };
            }

            JObject aleFieldSetting = null;
            if (featureConfiguration.TryGetValue(ALE_SETTING, out var aleFieldSettingRaw))
            {
                aleFieldSetting = aleFieldSettingRaw as JObject;
            }

            // Create and update the configuration feature settings
            dataSection.ConnectionString.Clear();
            if (featureConfiguration.TryGetValue(CONNECTION_PER_FEATURE_SETTING, out var itmSettingCollectionRaw) && itmSettingCollectionRaw is JObject itmSettingCollection)
            {
                foreach (var itm in this.m_dataConfigurationSections)
                {
                    if (itmSettingCollection.TryGetValue(itm.GetType().Name, out var itmSettingRaw) && itmSettingRaw is JObject itmSetting)
                    {
                        if (!itmSetting.TryGetValue(GLOBAL_DATA_PROVIDER_SETTING, out var providerRaw) || String.IsNullOrEmpty(providerRaw.ToString()))
                        {
                            // No provider set
                            continue;
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
                            this.SetAleConfigurationSettings(configuration, itm, aleFieldSetting);
                        }
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.DATA_STRUCTURE_NOT_APPROPRIATE, itm.GetType().Name, "invalid dictionary"));
                    }
                }
            }
            else if (featureConfiguration.TryGetValue(GLOBAL_DATA_PROVIDER_SETTING, out var providerRaw) &&
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
                    foreach (var itm in this.m_dataConfigurationSections)
                    {
                        itm.ReadonlyConnectionString = itm.ReadWriteConnectionString = "main";
                        this.SetAleConfigurationSettings(configuration, itm, aleFieldSetting);
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

            this.m_configuration = null;
            return true;
        }

        /// <summary>
        /// Update extended configuration options
        /// </summary>
        private void SetAleConfigurationSettings(SanteDBConfiguration configuration, OrmConfigurationBase configurationSection, JObject aleFieldSetting)
        {

            if (aleFieldSetting?.Value<bool>(ALE_ENABLED_SETTING) == true && configuration.ProtectedSectionKey != null)
            {
                configurationSection.AleConfiguration = new OrmAleConfiguration()
                {
                    AleEnabled = true,
                    EnableFields = (aleFieldSetting?.GetValue(ALE_FIELDS_SETTING) as JArray).Select(o => new OrmFieldConfiguration()
                    {
                        Mode = OrmAleMode.Deterministic,
                        Name = o.Value<String>()
                    }).ToList(),
                    Certificate = configuration.ProtectedSectionKey
                };
            }

        }
    }
}
