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
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Data.Backup;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// A configuration manager which uses a temporary configuration in memory 
    /// via implementations of <see cref="IInitialConfigurationProvider"/>
    /// </summary>
    public class InitialConfigurationManager : IConfigurationManager, IRequestRestarts, IRestoreBackupAssets
    {
        private static readonly Guid CONFIGURATION_FILE_ASSET_ID = Guid.Parse("09379015-3823-40F1-B051-573E9009E849");
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(InitialConfigurationManager));
        private SanteDBConfiguration m_configuration;
        private readonly string m_localConfigurationPath;

        /// <inheritdoc/>
        public event EventHandler RestartRequested;

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
        public Guid[] AssetClassIdentifiers => new[] { CONFIGURATION_FILE_ASSET_ID }; 

        /// <summary>
        /// Initial configuration manager 
        /// </summary>
        public InitialConfigurationManager(SanteDBHostType hostType, String instanceName, String fileLocation)
        {
            // Let the initial configuration providers do their magic
            var configuration = new SanteDBConfiguration()
            {
                Sections = new List<object>()
                {
                    new ApplicationServiceContextConfigurationSection()
                    {
                        ThreadPoolSize = Environment.ProcessorCount,
                        AllowUnsignedAssemblies = false,
                        InstanceName = instanceName,
                        ServiceProviders = new List<TypeReferenceConfiguration>()
                    }
                }
            };

            foreach (var initialProvider in AppDomain.CurrentDomain.GetAllTypes().Where(t => typeof(IInitialConfigurationProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).Select(t => Activator.CreateInstance(t)).OfType<IInitialConfigurationProvider>().OrderBy(o => o.Order))
            {
                this.m_tracer.TraceInfo("Initializing {0}...", initialProvider);
                configuration = initialProvider.Provide(hostType, configuration);
            }
            this.m_configuration = configuration;
            this.m_localConfigurationPath = fileLocation;
        }

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

            if (retVal == null)
            {
                throw new KeyNotFoundException(String.Format(ErrorMessages.CONNECTION_STRING_NOT_FOUND, key));
            }

            return retVal;
        }

        /// <inheritdoc/>
        public T GetSection<T>() where T : IConfigurationSection => this.m_configuration.GetSection<T>();

        /// <inheritdoc/>
        public void Reload()
        {
        }

        /// <inheritdoc/>
        public void SaveConfiguration(bool restart = true)
        {
            // Save configuration - 
            var encryptionCertificiate = this.m_configuration.GetSection<SecurityConfigurationSection>().Signatures.Find(o => o.KeyName == "default");
            if (encryptionCertificiate?.Algorithm != SignatureAlgorithm.HS256)
            {
                this.m_configuration.ProtectedSectionKey = new X509ConfigurationElement(encryptionCertificiate);
            }
            else
            {
                this.m_configuration.ProtectedSectionKey = null;
            }

            this.m_configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.RemoveAll(o => o.Type == typeof(InitialConfigurationManager));

            // Now we want to save
            using (var fs = File.Create(this.m_localConfigurationPath))
            {
                this.m_configuration.Save(fs);
            }

            if (restart)
            {
                this.RestartRequested?.Invoke(null, EventArgs.Empty);
            }

        }

        /// <inheritdoc/>
        public void SetAppSetting(string key, string value)
        {
            var appSettings = this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>();
            appSettings.AddAppSetting(key, value);
        }

        /// <inheritdoc/>
        public void SetTransientConnectionString(string name, ConnectionString connectionString)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public bool Restore(IBackupAsset backupAsset)
        {
            if (backupAsset.AssetClassId.Equals(CONFIGURATION_FILE_ASSET_ID))
            {
                using (var assetStream = backupAsset.Open())
                {
                    using (var configStream = File.Create(this.m_localConfigurationPath))
                    {
                        assetStream.CopyTo(configStream);
                        configStream.Seek(0, SeekOrigin.Begin);
                        this.m_configuration = SanteDBConfiguration.Load(configStream);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
