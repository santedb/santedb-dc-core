/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using Newtonsoft.Json.Linq;
using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Data;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Disconnected.Configuration
{
    /// <summary>
    /// Represents a client configuration feature for the SIM or MDM manager
    /// </summary>
    public class ResourceManagementConfigurationFeature : IClientConfigurationFeature
    {

        /// <summary>
        /// The selected manager
        /// </summary>
        public const string ENABLED_RES_MANAGER_SETTING = "manager";
        /// <summary>
        /// Resources under control of the manager
        /// </summary>
        public const string ENABLED_RES_SETTING = "resources";
        /// <summary>
        /// Deletion mode settings
        /// </summary>
        public const string DELETION_MODE_SETTING = "deletionMode";

        // Configuration section
        private ConfigurationDictionary<String, Object> m_configuration = null;
        private readonly ResourceManagementConfigurationSection m_configurationSection;
        private readonly Type[] m_dataManagementPatterns;
        private Type m_enabledDataManagementFeature;
        private readonly ModelSerializationBinder m_serializationBinder = new ModelSerializationBinder();

        /// <inheritdoc/>
        public int Order => Int32.MaxValue;

        /// <inheritdoc/>
        public string Name => "resourceManager";

        /// <summary>
        /// DI constructor
        /// </summary>
        public ResourceManagementConfigurationFeature(InitialConfigurationManager configurationManager)
        {
            this.m_configurationSection = configurationManager.GetSection<ResourceManagementConfigurationSection>();
            this.m_dataManagementPatterns = AppDomain.CurrentDomain.GetAllTypes().Where(t => !t.IsAbstract && !t.IsInterface && typeof(IDataManagementPattern).IsAssignableFrom(t)).ToArray();
            this.m_enabledDataManagementFeature = configurationManager.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.FirstOrDefault(o => m_dataManagementPatterns.Contains(o.Type))?.Type;
        }

        /// <inheritdoc/>
        public ConfigurationDictionary<string, object> Configuration => this.GetConfiguration();

        /// <summary>
        /// Get the configuration or return the loaded configuration
        /// </summary>
        private ConfigurationDictionary<string, object> GetConfiguration()
        {
            if(this.m_configuration == null)
            {
                this.m_configuration = new ConfigurationDictionary<string, object>();
                this.m_configuration.Add(DELETION_MODE_SETTING, this.m_configurationSection?.MasterDataDeletionMode ?? Core.Services.DeleteMode.PermanentDelete);
                this.m_configuration.Add(ENABLED_RES_MANAGER_SETTING, this.m_enabledDataManagementFeature?.AssemblyQualifiedNameWithoutVersion() ?? null);
                this.m_configuration.Add(ENABLED_RES_SETTING, this.m_configurationSection?.ResourceTypes.Select(o => o.TypeXml).ToArray() ?? new string[0]);
            }
            return this.m_configuration;
        }


        /// <inheritdoc/>
        public string ReadPolicy => PermissionPolicyIdentifiers.AlterSystemConfiguration;

        /// <inheritdoc/>
        public string WritePolicy => PermissionPolicyIdentifiers.AlterSystemConfiguration;

        /// <inheritdoc/>
        public bool Configure(SanteDBConfiguration configuration, IDictionary<string, object> featureConfiguration)
        {
            // Resource setting
            if(featureConfiguration.TryGetValue(ENABLED_RES_MANAGER_SETTING, out var resourceManagerRaw))
            {
                configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.RemoveAll(o => this.m_dataManagementPatterns.Contains(o.Type));
                configuration.RemoveSection<ResourceManagementConfigurationSection>();
                if (resourceManagerRaw != null)
                {
                    m_enabledDataManagementFeature = this.m_dataManagementPatterns.First(o => o.AssemblyQualifiedNameWithoutVersion() == resourceManagerRaw.ToString());
                    configuration.GetSection<ApplicationServiceContextConfigurationSection>().AddService(new TypeReferenceConfiguration(m_enabledDataManagementFeature));

                    var rmSection = configuration.AddSection(new ResourceManagementConfigurationSection());
                    if(featureConfiguration.TryGetValue(DELETION_MODE_SETTING, out var deletionSetting))
                    {
                        rmSection.MasterDataDeletionMode = deletionSetting.Equals("LogicalDelete") ? Core.Services.DeleteMode.LogicalDelete : Core.Services.DeleteMode.PermanentDelete;
                    }

                    if(featureConfiguration.TryGetValue(ENABLED_RES_SETTING, out var enabledResourcesRaw) && enabledResourcesRaw is JArray jar)
                    {
                        rmSection.ResourceTypes = jar.OfType<JToken>().Select(o => new ResourceTypeReferenceConfiguration(o.ToString())).ToList();
                    }
                    
                }
                return true;
            }
            return true;
        }
    }
}
