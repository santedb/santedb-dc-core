/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */

using SanteDB.BI.Services.Impl;
using SanteDB.Cdss.Xml;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Protocol;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Privacy;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Ags;
using SanteDB.DisconnectedClient.Backup;
using SanteDB.DisconnectedClient.Caching;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Diagnostics;
using SanteDB.DisconnectedClient.Http;
using SanteDB.DisconnectedClient.Net;
using SanteDB.DisconnectedClient.Rules;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Security.Remote;
using SanteDB.DisconnectedClient.Security.Session;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Services.Local;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.DisconnectedClient.Tickler;
using SanteDB.DisconnectedClient.UI.Services;
using SharpCompress.Compressors.BZip2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                return File.Exists(this.m_configPath) || this.AttemptRestore();
            }
        }

        /// <summary>
        /// Attempt restore from windows.old
        /// </summary>
        /// <remarks>Sometimes Windows Update will remove our configuration in %SYSTEMPROFILE%\ which makes the
        /// DCG think it isn't configured. This routine will check WINDOWS.OLD and copy the configuration files
        /// over if they exist</remarks>
        private bool AttemptRestore()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Path in Windows.old
                var oldPath = this.m_configPath.Replace(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Path.Combine(Path.ChangeExtension(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "OLD"), "Windows"))
                    .ToUpper();

                if (Environment.Is64BitOperatingSystem && Environment.Is64BitProcess)
                    oldPath = oldPath.Replace("SYSTEM32", "SYSWOW64") // HACK: System folders are rewritten but the backup folders are not
                    ;

                try
                {
                    Debug.WriteLine($"New configuration at {this.m_configPath} doesn't exist", "RESTORE_UPDATE");
                    Debug.WriteLine($"Checking for old configuration at {oldPath}...", "RESTORE_UPDATE");

                    if (File.Exists(oldPath))
                    {
                        Debug.WriteLine($"Old configuration at {oldPath} found, will restore...", "RESTORE_UPDATE");

                        // Copy the config file
                        if (!Directory.Exists(Path.GetDirectoryName(this.m_configPath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(this.m_configPath));
                        File.Copy(oldPath, this.m_configPath);

                        // Next copy the data directory
                        var config = this.Load();

                        var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SanteDB", this.m_instanceName);
                        var oldDataPath = dataPath.Replace(
                            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                            Path.Combine(Path.ChangeExtension(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "OLD"), "Windows"))
                            .ToUpper()
                            .Replace("SYSTEM32", "SYSWOW64");

                        if (Directory.Exists(dataPath))
                            Directory.Delete(dataPath, true);
                        Directory.Move(oldDataPath, dataPath);
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"No configuration at {oldPath} to restore...", "RESTORE_UPDATE");

                        return false;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"ERROR: Checking for old configuration at {oldPath}...", "RESTORE_UPDATE");
                    throw new Exception($"Could not restore files from Windows.Old please consult system administrator", e);
                }
            }
            else
                return false;
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
                ThreadPoolSize = Environment.ProcessorCount * 16,
                ServiceProviders = new List<TypeReferenceConfiguration>() {
                    new TypeReferenceConfiguration(typeof(AesSymmetricCrypographicProvider)),
                    new TypeReferenceConfiguration(typeof(MemoryTickleService)),
                    new TypeReferenceConfiguration(typeof(SHA256PasswordHasher)),
                    new TypeReferenceConfiguration(typeof(SanteDB.Core.Security.DefaultPolicyDecisionService)),
                    new TypeReferenceConfiguration(typeof(DataPolicyFilterService)),
                    new TypeReferenceConfiguration(typeof(NetworkInformationService)),
                    new TypeReferenceConfiguration(typeof(BusinessRulesDaemonService)),
                    new TypeReferenceConfiguration(typeof(AgsService)),
                    new TypeReferenceConfiguration(typeof(SanteDB.Caching.Memory.MemoryCacheService)),
                    new TypeReferenceConfiguration(typeof(NetThreadPoolService)),
                    new TypeReferenceConfiguration(typeof(SimpleCarePlanService)),
                    new TypeReferenceConfiguration(typeof(MemorySessionManagerService)),
                    new TypeReferenceConfiguration(typeof(AmiUpdateManager)),
                    new TypeReferenceConfiguration(typeof(AppletClinicalProtocolRepository)),
                    new TypeReferenceConfiguration(typeof(AppletLocalizationService)),
                    new TypeReferenceConfiguration(typeof(MemoryQueryPersistenceService)),
                    new TypeReferenceConfiguration(typeof(AuditDaemonService)),
                    new TypeReferenceConfiguration(typeof(SimpleQueueFileProvider)),
                    new TypeReferenceConfiguration(typeof(SimplePatchService)),
                    new TypeReferenceConfiguration(typeof(DefaultBackupService)),
                    new TypeReferenceConfiguration(typeof(DcAppletManagerService)),
                    new TypeReferenceConfiguration(typeof(AppletBiRepository)),
                    new TypeReferenceConfiguration(typeof(DefaultOperatingSystemInfoService)),
                    new TypeReferenceConfiguration(typeof(AppletSubscriptionRepository)),
                    new TypeReferenceConfiguration(typeof(AmiSecurityChallengeProvider)),
                    new TypeReferenceConfiguration(typeof(InMemoryPivotProvider)),
                    new TypeReferenceConfiguration(typeof(DefaultDataSigningService)),
                    new TypeReferenceConfiguration(typeof(GenericConfigurationPushService)),
                    new TypeReferenceConfiguration(typeof(QrBarcodeGenerator)),
                    new TypeReferenceConfiguration(typeof(FileSystemDispatcherQueueService))
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
            retVal.Sections.Add(new FileSystemDispatcherQueueConfigurationSection()
            {
                QueuePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SanteDB", this.m_instanceName, "queue"),
            });
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

            foreach (var t in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a =>
                {
                    try
                    {
                        return a.ExportedTypes;
                    }
                    catch (Exception)
                    {
                        return Type.EmptyTypes;
                    }
                })
                .Where(t => typeof(IInitialConfigurationProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
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
            if (this.IsConfigured && File.Exists(this.m_configPath))
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
                throw new Exception($"Unable to save configuration to {this.m_configPath}", e);
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
            try
            {
                // HACK: For some reason the DCG doesn't like to backup the configuration file
                using (var lzs = new BZip2Stream(File.Create(Path.ChangeExtension(this.m_configPath, "bak.bz2")), SharpCompress.Compressors.CompressionMode.Compress, false))
                    configuration.Save(lzs);
            }
            catch (Exception e)
            {
                File.Delete(Path.ChangeExtension(this.m_configPath, "bak.bz2"));
                throw new InvalidOperationException($"Could not backup to {Path.ChangeExtension(this.m_configPath, "bak.bz2")}", e);
            }
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
                ApplicationContext.Current.ConfigurationManager?.Reload();
                return retVal;
            }
        }
    }
}