﻿using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// A configuration manager which uses a temporary configuration in memory 
    /// via implementations of <see cref="IInitialConfigurationProvider"/>
    /// </summary>
    public class InitialConfigurationManager : IConfigurationManager, IRequestRestarts
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(InitialConfigurationManager));
        private readonly SanteDBConfiguration m_configuration;
        private readonly string m_localConfigurationPath;

        // Restart requested
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

            foreach(var initialProvider in AppDomain.CurrentDomain.GetAllTypes().Where(t=>typeof(IInitialConfigurationProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).Select(t=>Activator.CreateInstance(t)).OfType<IInitialConfigurationProvider>())
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
            // Save configuration - 
            var encryptionCertificiate = this.m_configuration.GetSection<SecurityConfigurationSection>().Signatures.Find(o=>o.KeyName == "default");
            if(encryptionCertificiate?.Algorithm != SignatureAlgorithm.HS256)
            {
                this.m_configuration.ProtectedSectionKey = new X509ConfigurationElement(encryptionCertificiate);
            }

            this.m_configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.RemoveAll(o => o.Type == typeof(InitialConfigurationManager));
            
            // Now we want to save
            using(var fs = File.Create(this.m_localConfigurationPath))
            {
                this.m_configuration.Save(fs);
            }
            this.RestartRequested?.Invoke(null, EventArgs.Empty);

        }

        /// <inheritdoc/>
        public void SetAppSetting(string key, string value)
        {
            var appSettings = this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>();
            appSettings.AddAppSetting(key, value);
        }
    }
}
