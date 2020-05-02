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
using SanteDB.BI.Services.Impl;
using SanteDB.Cdss.Xml;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Protocol;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Ags;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Caching;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Security.Remote;
using SanteDB.DisconnectedClient.Core.Security.Session;
using SanteDB.DisconnectedClient.Core.Services.Local;
using SanteDB.DisconnectedClient.Core.Synchronization;
using SanteDB.DisconnectedClient.Core.Tickler;
using SanteDB.DisconnectedClient.Xamarin.Backup;
using SanteDB.DisconnectedClient.Xamarin.Configuration;
using SanteDB.DisconnectedClient.Xamarin.Diagnostics;
using SanteDB.DisconnectedClient.Xamarin.Http;
using SanteDB.DisconnectedClient.Xamarin.Net;
using SanteDB.DisconnectedClient.Xamarin.Rules;
using SanteDB.DisconnectedClient.Xamarin.Security;
using SanteDB.DisconnectedClient.Xamarin.Services;
using SanteDB.DisconnectedClient.Xamarin.Threading;
using SharpCompress.Compressors.BZip2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.DisconnectedClient.UI

{
    /// <summary>
    /// Configuration manager
    /// </summary>
    internal class DcConfigurationManager : IConfigurationPersister
    {
        private const int PROVIDER_RSA_FULL = 1;

        // Tracer
        private Tracer m_tracer;

        // Configuration path
        private readonly String m_configPath;

        // The name of the instance
        private readonly String m_instanceName;

        /// <summary>
        /// Returns true if SanteDB is configured
        /// </summary>
        /// <value><c>true</c> if this instance is configured; otherwise, <c>false</c>.</value>
        public bool IsConfigured
        {
            get
            {
                return File.Exists(this.m_configPath);
            }
        }

        /// <summary>
        /// Get a bare bones configuration
        /// </summary>
        public SanteDBConfiguration GetDefaultConfiguration()
        {
            // TODO: Bring up initial settings dialog and utility
            var retVal = new SanteDBConfiguration();
            
            // Initial Applet configuration
            AppletConfigurationSection appletSection = new AppletConfigurationSection()
            {
                AppletDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SanteDB", this.m_instanceName, "applets"),
                StartupAsset = "org.santedb.uicore",
                Security = new AppletSecurityConfiguration()
                {
                    AllowUnsignedApplets = true,
                    TrustedPublishers = new List<string>() { "82C63E1E9B87578D0727E871D7613F2F0FAF683B", "4326A4421216AC254DA93DC61B93160B08925BB1" }
                }
            };

            // Initial applet style
            ApplicationConfigurationSection appSection = new ApplicationConfigurationSection()
            {
                Style = StyleSchemeType.Dark,
                UserPrefDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SanteDB", this.m_instanceName, "userpref"),
                Cache = new CacheConfiguration()
                {
                    MaxAge = new TimeSpan(0, 5, 0).Ticks,
                    MaxSize = 1000,
                    MaxDirtyAge = new TimeSpan(0, 20, 0).Ticks,
                    MaxPressureAge = new TimeSpan(0, 2, 0).Ticks
                }
            };

            // App service 
            var appServiceSection = new ApplicationServiceContextConfigurationSection()
            {
                ThreadPoolSize = Environment.ProcessorCount * 2,
                ServiceProviders = new List<TypeReferenceConfiguration>() {
                    new TypeReferenceConfiguration(typeof(AesSymmetricCrypographicProvider)),
                    new TypeReferenceConfiguration(typeof(MemoryTickleService)),
                    new TypeReferenceConfiguration(typeof(DefaultPolicyDecisionService)),
                    new TypeReferenceConfiguration(typeof(NetworkInformationService)),
                    new TypeReferenceConfiguration(typeof(BusinessRulesDaemonService)),
                    new TypeReferenceConfiguration(typeof(AgsService)),
                    new TypeReferenceConfiguration(typeof(MemoryCacheService)),
                    new TypeReferenceConfiguration(typeof(SanteDBThreadPool)),
                    new TypeReferenceConfiguration(typeof(SimpleCarePlanService)),
                    new TypeReferenceConfiguration(typeof(MemorySessionManagerService)),
                    new TypeReferenceConfiguration(typeof(AmiUpdateManager)),
                    new TypeReferenceConfiguration(typeof(AppletClinicalProtocolRepository)),
                    new TypeReferenceConfiguration(typeof(MemoryQueryPersistenceService)),
                    new TypeReferenceConfiguration(typeof(AuditDaemonService)),
                    new TypeReferenceConfiguration(typeof(SimpleQueueFileProvider)),
                    new TypeReferenceConfiguration(typeof(SimplePatchService)),
                    new TypeReferenceConfiguration(typeof(XamarinBackupService)),
                    new TypeReferenceConfiguration(typeof(DcAppletManagerService)),
                    new TypeReferenceConfiguration(typeof(AppletBiRepository)),
                    new TypeReferenceConfiguration(typeof(DefaultOperatingSystemInfoService)),
                    new TypeReferenceConfiguration(typeof(AppletSubscriptionRepository)),
                    new TypeReferenceConfiguration(typeof(AmiSecurityChallengeProvider)),
                    new TypeReferenceConfiguration(typeof(InMemoryPivotProvider)),
                    new TypeReferenceConfiguration(typeof(DefaultDataSigningService))
                }
            };

            // Security configuration
            SecurityConfigurationSection secSection = new SecurityConfigurationSection()
            {
                DeviceName = Environment.MachineName,
                AuditRetention = new TimeSpan(30, 0, 0, 0, 0),
                DomainAuthentication = DomainClientAuthentication.Inline
            };

            // Device key
            //var certificate = X509CertificateUtils.FindCertificate(X509FindType.FindBySubjectName, StoreLocation.LocalMachine, StoreName.My, String.Format("DN={0}.mobile.santedb.org", macAddress));
            //secSection.DeviceSecret = certificate?.Thumbprint;

            // Rest Client Configuration
            ServiceClientConfigurationSection serviceSection = new ServiceClientConfigurationSection()
            {
                RestClientType = typeof(RestClient)
            };

            // Trace writer
#if DEBUG
            DiagnosticsConfigurationSection diagSection = new DiagnosticsConfigurationSection()
            {
                TraceWriter = new System.Collections.Generic.List<TraceWriterConfiguration>() {
                    new TraceWriterConfiguration () {
                        Filter = System.Diagnostics.Tracing.EventLevel.Informational,
                        InitializationData = "SanteDB",
                        TraceWriter = typeof(LogTraceWriter)
                    },
                    new TraceWriterConfiguration() {
                        Filter = System.Diagnostics.Tracing.EventLevel.Informational,
                        InitializationData = "SanteDB",
                        TraceWriter = typeof(FileTraceWriter)
                    }
                }
            };
#else
            DiagnosticsConfigurationSection diagSection = new DiagnosticsConfigurationSection()
            {
                TraceWriter = new List<TraceWriterConfiguration>() {
                    new TraceWriterConfiguration () {
                        Filter = System.Diagnostics.Tracing.EventLevel.Warning,
                        InitializationData = "SanteDB",
                        TraceWriter = typeof(FileTraceWriter)
                    }
                }
            };
#endif
            retVal.Sections.Add(appServiceSection);
            retVal.Sections.Add(appletSection);
            retVal.Sections.Add(diagSection);
            retVal.Sections.Add(appSection);
            retVal.Sections.Add(secSection);
            retVal.Sections.Add(serviceSection);
            retVal.Sections.Add(new AuditAccountabilityConfigurationSection()
            {
                AuditFilters = new List<AuditFilterConfiguration>()
                {
                    // Audit any failure - No matter which event
                    new AuditFilterConfiguration(null, null, SanteDB.Core.Auditing.OutcomeIndicator.EpicFail | SanteDB.Core.Auditing.OutcomeIndicator.MinorFail | SanteDB.Core.Auditing.OutcomeIndicator.SeriousFail, true, true),
                    // Audit anything that creates, reads, or updates data
                    new AuditFilterConfiguration(SanteDB.Core.Auditing.ActionType.Create | SanteDB.Core.Auditing.ActionType.Read | SanteDB.Core.Auditing.ActionType.Update | SanteDB.Core.Auditing.ActionType.Delete, null, null, true, true)
                }
            });

            retVal.Sections.Add(new DcDataConfigurationSection()
            {
                MainDataSourceConnectionStringName = "santeDbData",
                MessageQueueConnectionStringName = "santeDbQueue"
            });
            retVal.AddSection(AgsService.GetDefaultConfiguration());
            retVal.Sections.Add(new SynchronizationConfigurationSection()
            {
                PollInterval = new TimeSpan(0, 5, 0),
                ForbiddenResouces = new List<SynchronizationForbidConfiguration>()
                {
                    new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "DeviceEntity"),
                    new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "ApplicationEntity"),
                    new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "Concept"),
                    new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "ConceptSet"),
                    new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "Place"),
                    new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "ReferenceTerm"),
                    new SynchronizationForbidConfiguration(SynchronizationOperationType.All, "AssigningAuthority"),
                    new SynchronizationForbidConfiguration(SynchronizationOperationType.Obsolete, "UserEntity")
                }
            });

            foreach (var t in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).SelectMany(a => a.ExportedTypes).Where(t => typeof(IInitialConfigurationProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
                retVal = (Activator.CreateInstance(t) as IInitialConfigurationProvider).Provide(retVal);
            return retVal;
        }


        /// <summary>
        /// Creates a new instance of the configuration manager with the specified configuration file
        /// </summary>
        public DcConfigurationManager(String instanceName)
        {
            this.m_instanceName = instanceName;
            this.m_configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SanteDB", instanceName, "SanteDB.config");
        }

        /// <summary>
        /// Load the configuration
        /// </summary>
        public SanteDBConfiguration Load()
        {
            // Configuration exists?
            if (this.IsConfigured)
                using (var fs = File.OpenRead(this.m_configPath))
                {
                    return SanteDBConfiguration.Load(fs);
                }
            else
                return this.GetDefaultConfiguration();
        }

        /// <summary>
        /// Save the specified configuration
        /// </summary>
        /// <param name="config">Config.</param>
        public void Save(SanteDBConfiguration config)
        {
            try
            {
                this.m_tracer?.TraceInfo("Saving configuration to {0}...", this.m_configPath);
                if (!Directory.Exists(Path.GetDirectoryName(this.m_configPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(this.m_configPath));

                using (FileStream fs = File.Create(this.m_configPath))
                {
                    config.Save(fs);
                    fs.Flush();
                }
            }
            catch (Exception e)
            {
                this.m_tracer?.TraceError(e.ToString());
            }
        }

        /// <summary>
        /// Application data directory
        /// </summary>
        public string ApplicationDataDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SanteDB", this.m_instanceName);
            }
        }


        /// <summary>
        /// Backup the configuration
        /// </summary>
        public void Backup(SanteDBConfiguration configuration)
        {
            using (var lzs = new BZip2Stream(File.Create(Path.ChangeExtension(this.m_configPath, "bak.bz2")), SharpCompress.Compressors.CompressionMode.Compress, false))
                configuration.Save(lzs);
        }

        /// <summary>
        /// True if the configuration has a backup
        /// </summary>
        public bool HasBackup()
        {
            return File.Exists(Path.ChangeExtension(this.m_configPath, "bak.bz2"));
        }

        /// <summary>
        /// Restore the configuration
        /// </summary>
        public SanteDBConfiguration Restore()
        {
            using (var lzs = new BZip2Stream(File.OpenRead(Path.ChangeExtension(this.m_configPath, "bak.bz2")), SharpCompress.Compressors.CompressionMode.Decompress, false))
            {
                var retVal = SanteDBConfiguration.Load(lzs);
                this.Save(retVal);
                ApplicationContext.Current.ConfigurationManager.Reload();
                return retVal;
            }
        }

    }
}