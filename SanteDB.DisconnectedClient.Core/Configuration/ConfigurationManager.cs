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
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Configuration
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
            var persister = (defaultPersister ?? ApplicationContext.Current.ConfigurationPersister);
            try
            {
                this.Configuration = persister.Load();
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not load configuration file - Will attempt restore - {0}", e);
                try {
                    if(persister.HasBackup())
                        (defaultPersister ?? ApplicationContext.Current.ConfigurationPersister).Restore();
                    this.Configuration = persister.Load();
                }
                catch(Exception e2)
                {
                    throw new InvalidOperationException("Cannot load configuration file", e2);
                }
            }
        }
 
        /// <summary>
        /// Get app setting
        /// </summary>
        public string GetAppSetting(string key)
        {
            return this.GetSection<ApplicationServiceContextConfigurationSection>()?.AppSettings?.Find(o => o.Key == key)?.Value;
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
        public ConnectionString GetConnectionString(String name)
        {
            var dcs = this.GetSection<DcDataConfigurationSection>();
            var cs = dcs?.ConnectionString.Find(o => o.Name == name);
            if (cs == null)
                return null;
            else
                return cs.Clone();
        }

        /// <summary>
        /// Set application setting
        /// </summary>
        public void SetAppSetting(string key, String value)
        {
            var setting = this.GetSection<ApplicationServiceContextConfigurationSection>()?.AppSettings?.Find(o => o.Key == key);
            if (setting == null)
                this.GetSection<ApplicationServiceContextConfigurationSection>()?.AppSettings.Add(new AppSettingKeyValuePair() { Key = key, Value = value.ToString() });
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

