﻿using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Disconnected.Configuration
{
    /// <summary>
    /// A configuration manager which uses a temporary configuration in memory
    /// </summary>
    public class TemporaryConfigurationManager : IConfigurationManager
    {
        private readonly SanteDBConfiguration m_configuration;

        /// <summary>
        /// True if the configuration is readonly
        /// </summary>
        public bool IsReadonly => true;

        /// <summary>
        /// Gets the configuration
        /// </summary>
        public SanteDBConfiguration Configuration => this.m_configuration;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Temporary Configuration Service";

        /// <inheritdoc/>
        public string GetAppSetting(string key)
        {
            // Use configuration setting 
            string retVal = null;
            try
            {
                retVal = Configuration.GetSection<ApplicationServiceContextConfigurationSection>()?.AppSettings.Find(o => o.Key == key)?.Value;
            }
            catch
            {
            }

            return retVal;
        }

        /// <inheritdoc/>
        public ConnectionString GetConnectionString(string key)
        {
            // Use configuration setting 
            ConnectionString retVal = null;
            try
            {
                retVal = Configuration.GetSection<DataConfigurationSection>()?.ConnectionString.Find(o => o.Name == key);
            }
            catch { }

            return retVal;
        }

        /// <inheritdoc/>
        public T GetSection<T>() where T : IConfigurationSection => this.m_configuration.GetSection<T>();

        /// <inheritdoc/>
        public void Reload()
        {
        }

        /// <inheritdoc/>
        public void SaveConfiguration()
        {
        }

        /// <inheritdoc/>
        public void SetAppSetting(string key, string value)
        {
            var appSettings = this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>().AppSettings;
            appSettings.RemoveAll(o => o.Key == key);
            appSettings.Add(new AppSettingKeyValuePair(key, value));
        }
    }
}
