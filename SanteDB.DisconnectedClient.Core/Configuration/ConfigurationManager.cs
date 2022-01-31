/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;

namespace SanteDB.DisconnectedClient.Configuration
{

    /// <summary>
    /// Disconnected Client Configuration Manager
    /// </summary>
    /// <remarks>
    /// <para>This configuration manager is responsible for loading configuration files from the <c>%appdata%\santedb</c> 
    /// (or <c>~/.config/santedb</c> on Linux and MacOS) directory  matching the dCDR technology (Web Access Gateway, 
    /// Disconnected Client, Windows, Linux or Android apps) and the named instance data.</para>
    /// </remarks>
    [Description("dCDR Configuration Manager")]
    public class ConfigurationManager : IConfigurationManager
    {
        /// <summary>
        /// SanteDB configuration
        /// </summary>
        public ConfigurationManager(IConfigurationPersister defaultPersister = null)
        {
            var persister = defaultPersister ?? ApplicationContext.Current.ConfigurationPersister;
            try
            {
                this.Configuration = persister.Load();
            }
            catch (ConfigurationException e)
            {
                Trace.TraceError("Could not load configuration file");
                if (persister.HasBackup())
                {
                    Trace.TraceInformation("Will attempt to restore persisted backup");
                    persister.Restore();
                    this.Configuration = persister.Load();
                }
                else
                {
                    Trace.TraceInformation("No backup could be found, attempting to correct configuration issues");
                    e.Configuration.Sections.RemoveAll(o => o is XmlNode[]);
                    persister.Save(e.Configuration);
                    this.Configuration = e.Configuration;
                    Trace.TraceInformation("No backup could be found, attempting to correct configuration issues");
                }
            }

        }

        /// <summary>
        /// Gets the configuration object
        /// </summary>
        public SanteDBConfiguration Configuration { get; private set; }

        /// <summary>
        /// Get app setting
        /// </summary>
        public string GetAppSetting(string key)
        {
            return this.GetSection<ApplicationServiceContextConfigurationSection>()?.AppSettings?.Find(o => o.Key == key)?.Value;
        }

        /// <summary>
        /// Get connection string
        /// </summary>
        /// <returns>The connection string.</returns>
        /// <param name="name">Name.</param>
        public ConnectionString GetConnectionString(string name)
        {
            var dcs = this.GetSection<DcDataConfigurationSection>();
            var cs = dcs?.ConnectionString.Find(o => o.Name == name);
            if (cs == null)
            {
                return null;
            }

            return cs.Clone();
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
        /// Reload the configuration
        /// </summary>
        public void Reload()
        {
            this.Configuration = ApplicationContext.Current.ConfigurationPersister.Load();
        }

        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "Default Disconnected Client Configuration Manager";

        /// <summary>
        /// Set application setting
        /// </summary>
        public void SetAppSetting(string key, string value)
        {
            var setting = this.GetSection<ApplicationServiceContextConfigurationSection>()?.AppSettings?.Find(o => o.Key == key);
            if (setting == null)
            {
                this.GetSection<ApplicationServiceContextConfigurationSection>()?.AppSettings.Add(new AppSettingKeyValuePair { Key = key, Value = value });
            }
            else
            {
                setting.Value = value;
            }

            if (ApplicationContext.Current.ConfigurationPersister.IsConfigured)
            {
                ApplicationContext.Current.ConfigurationPersister.Save(this.Configuration);
            }
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
    }
}

