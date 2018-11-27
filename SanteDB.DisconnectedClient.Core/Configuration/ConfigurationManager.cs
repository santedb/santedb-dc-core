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
 * Date: 2018-11-19
 */
using SanteDB.Core.Configuration;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Core.Configuration
{

    /// <summary>
    /// Configuration service 
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Default Disconnected Client Configuration Manager";

        /// <summary>
        /// Gets the configuration object
        /// </summary>
        public SanteDBConfiguration Configuration { get; private set; }

        /// <summary>
        /// SanteDB configuration
        /// </summary>
        public ConfigurationManager(IConfigurationPersister defaultPersister = null)
        {
            this.Configuration = (defaultPersister  ?? ApplicationContext.Current.ConfigurationPersister).Load();
        }

        /// <summary>
        /// Get app setting
        /// </summary>
        public string GetAppSetting(string key)
        {
            return this.GetSection<ApplicationConfigurationSection>()?.AppSettings?.Find(o => o.Key == key)?.Value;
        }
        
        /// <summary>
        /// Get the specified section
        /// </summary>
        /// <returns>The section.</returns>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public T GetSection<T>() where T : IConfigurationSection
        {
            return this.Configuration.GetSection<T>();
        }

        /// <summary>
        /// Gets the section of specified type.
        /// </summary>
        /// <returns>The section.</returns>
        /// <param name="t">T.</param>
        public object GetSection(Type t)
        {
            return this.Configuration.GetSection(t);
        }

        /// <summary>
        /// Get connection string
        /// </summary>
        /// <returns>The connection string.</returns>
        /// <param name="name">Name.</param>
        public ConnectionStringInfo GetConnectionString(String name)
        {
            var dcs = this.GetSection<DataConfigurationSection>();
            var cs = dcs?.ConnectionString.Find(o => o.Name == name);
            if (cs == null)
                return null;
            else
                return new ConnectionStringInfo(dcs.Provider, cs.Value);
        }

        /// <summary>
        /// Set application setting
        /// </summary>
        public void SetAppSetting(string key, String value)
        {
            var setting = this.GetSection<ApplicationConfigurationSection>()?.AppSettings?.Find(o => o.Key == key);
            if (setting == null)
                this.GetSection<ApplicationConfigurationSection>()?.AppSettings.Add(new AppSettingKeyValuePair() { Key = key, Value = value.ToString() });
            else
                setting.Value = value.ToString();

            if(ApplicationContext.Current.ConfigurationPersister.IsConfigured)
                ApplicationContext.Current.ConfigurationPersister.Save(this.Configuration);
        }

        /// <summary>
        /// Reload the configuration
        /// </summary>
        public void Reload()
        {
            this.Configuration = ApplicationContext.Current.ConfigurationPersister.Load();
        }
    }
}

