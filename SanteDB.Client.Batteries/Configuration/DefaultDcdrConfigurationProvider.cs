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
using SanteDB.BI.Services.Impl;
using SanteDB.BusinessRules.JavaScript;
using SanteDB.Caching.Memory;
using SanteDB.Caching.Memory.Session;
using SanteDB.Client.Batteries.Services;
using SanteDB.Client.Configuration;
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Client.OAuth;
using SanteDB.Client.Tickles;
using SanteDB.Client.Upstream;
using SanteDB.Client.Upstream.Management;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Client.Upstream.Security;
using SanteDB.Core;
using SanteDB.Core.Applets.Configuration;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Http;
using SanteDB.Core.Data;
using SanteDB.Core.Data.Backup;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Diagnostics.Tracing;
using SanteDB.Core.Http;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Privacy;
using SanteDB.Core.Services.Impl;
using SanteDB.Rest.OAuth.Configuration;
using SanteDB.Security.Certs.BouncyCastle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.Client.Batteries.Configuration
{
    /// <summary>
    /// Default DCDR configuration provider
    /// </summary>
    public class DefaultDcdrConfigurationProvider : IInitialConfigurationProvider
    {
        /// <inheritdoc/>
        public int Order => Int32.MinValue;

        /// <inheritdoc/>
        public SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration)
        {
            if (ClientBatteries.UpstreamCredentials == null)
            {
                throw new InvalidOperationException("Call ClientBatteries.Initialize()");
            }

            var appServiceSection = configuration.GetSection<ApplicationServiceContextConfigurationSection>();
            var instanceName = appServiceSection.InstanceName;
            var localDataPath = AppDomain.CurrentDomain.GetData(ClientBatteries.DATA_DIRECTORY).ToString();

            appServiceSection.ServiceProviders.AddRange(new List<TypeReferenceConfiguration>() {
                    new TypeReferenceConfiguration(typeof(AesSymmetricCrypographicProvider)),
                    new TypeReferenceConfiguration(typeof(InMemoryTickleService)),
                    new TypeReferenceConfiguration(typeof(DefaultNetworkInformationService)),
                    new TypeReferenceConfiguration(typeof(SHA256PasswordHashingService)),
                    new TypeReferenceConfiguration(typeof(DefaultPolicyDecisionService)),
                    new TypeReferenceConfiguration(typeof(MemoryAdhocCacheService)),
                    new TypeReferenceConfiguration(typeof(AppletLocalizationService)),
                    new TypeReferenceConfiguration(typeof(AppletBusinessRulesDaemon)),
                    new TypeReferenceConfiguration(typeof(DefaultUpstreamManagementService)),
                    new TypeReferenceConfiguration(typeof(DefaultUpstreamIntegrationService)),
                    new TypeReferenceConfiguration(typeof(DefaultUpstreamAvailabilityProvider)),
                    new TypeReferenceConfiguration(typeof(MemoryCacheService)),
                    new TypeReferenceConfiguration(typeof(DefaultThreadPoolService)),
                    new TypeReferenceConfiguration(typeof(MemoryQueryPersistenceService)),
                    new TypeReferenceConfiguration(typeof(FileSystemDispatcherQueueService)),
                    new TypeReferenceConfiguration(typeof(SimplePatchService)),
                    new TypeReferenceConfiguration(typeof(DefaultBackupManager)),
                    new TypeReferenceConfiguration(typeof(ClientAppletManagerService)),
                    new TypeReferenceConfiguration(typeof(AppletBiRepository)),
                    new TypeReferenceConfiguration(typeof(OAuthClient)),
                    new TypeReferenceConfiguration(typeof(MemorySessionManagerService)),
                    new TypeReferenceConfiguration(typeof(UpstreamUpdateManagerService)), // AmiUpdateManager
                    new TypeReferenceConfiguration(typeof(UpstreamIdentityProvider)),
                    new TypeReferenceConfiguration(typeof(UpstreamTfaService)),
                    new TypeReferenceConfiguration(typeof(UpstreamApplicationIdentityProvider)),
                    new TypeReferenceConfiguration(typeof(UpstreamSecurityChallengeProvider)), // AmiSecurityChallengeProvider
                    new TypeReferenceConfiguration(typeof(UpstreamRoleProviderService)),
                    new TypeReferenceConfiguration(typeof(UpstreamSecurityRepository)),
                    new TypeReferenceConfiguration(typeof(UpstreamRepositoryFactory)),
                    new TypeReferenceConfiguration(typeof(InitialAppletInstallationService)),
                    new TypeReferenceConfiguration(typeof(UpstreamPolicyInformationService)),
                    new TypeReferenceConfiguration(typeof(DataPolicyFilterService)),
                    new TypeReferenceConfiguration(typeof(DefaultOperatingSystemInfoService)),
                    new TypeReferenceConfiguration(typeof(AppletSubscriptionRepository)),
                    new TypeReferenceConfiguration(typeof(InMemoryPivotProvider)),
                    new TypeReferenceConfiguration(typeof(AuditDaemonService)),
                    new TypeReferenceConfiguration(typeof(DefaultDataSigningService)),
                    new TypeReferenceConfiguration(typeof(DefaultBarcodeProviderService)),
                    new TypeReferenceConfiguration(typeof(FileSystemDispatcherQueueService)),
                    new TypeReferenceConfiguration(typeof(BouncyCastleCertificateGenerator)),
                    new TypeReferenceConfiguration(typeof(RepositoryEntitySource))
            });

            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("input.name", "simple"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("input.address", "text"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("optional.patient.address.city", "true"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("optional.patient.address.county", "true"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("optional.patient.address.state", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("optional.patient.name.family", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("optional.patient.address.given", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("forbid.patient.address.state", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("forbid.patient.address.county", "true"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("forbid.patient.address.city", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("forbid.patient.address.precinct", "true"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("forbid.patient.name.prefix", "true"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("forbid.patient.name.suffix", "true"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("forbid.patient.name.family", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("forbid.patient.name.given", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("allow.patient.religion", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("allow.patient.ethnicity", "false"));
            appServiceSection.AppSettings.Add(new AppSettingKeyValuePair("aa.preferred", ""));
            appServiceSection.AppSettings = appServiceSection.AppSettings.OrderBy(o => o.Key).ToList();


            // Upstream default configuration
            UpstreamConfigurationSection upstreamConfiguration = new UpstreamConfigurationSection()
            {
                Credentials = new List<UpstreamCredentialConfiguration>()
                {
                    new UpstreamCredentialConfiguration()
                    {
                        CredentialName = Environment.MachineName,
                        Conveyance = UpstreamCredentialConveyance.Secret,
                        CredentialType = UpstreamCredentialType.Device
                    },
                    ClientBatteries.UpstreamCredentials
                }
            };



            configuration.AddSection(new SecurityConfigurationSection()
            {
                PasswordRegex = @"^(?=.*\d){1,}(?=.*[a-z]){1,}(?=.*[A-Z]){1,}(?=.*[^\w\d]){1,}.{6,}$",
                SecurityPolicy = new List<SecurityPolicyConfiguration>()
                {
                    new SecurityPolicyConfiguration(SecurityPolicyIdentification.SessionLength, new TimeSpan(0,30,0)),
                    new SecurityPolicyConfiguration(SecurityPolicyIdentification.RefreshLength, new TimeSpan(0,35,0))
                },
                Signatures = new List<SanteDB.Core.Security.Configuration.SecuritySignatureConfiguration>()
                {
                    new SanteDB.Core.Security.Configuration.SecuritySignatureConfiguration()
                    {
                        KeyName ="default",
                        Algorithm = SanteDB.Core.Security.Configuration.SignatureAlgorithm.HS256,
                        HmacSecret = $"@SanteDBDefault$$${DateTime.Now.Year}_{Environment.MachineName}_{Guid.NewGuid()}"

                    }
                }
            });
            // Trace writer

            var logDirectory = Path.Combine(localDataPath, "log");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

#if DEBUG
            DiagnosticsConfigurationSection diagSection = new DiagnosticsConfigurationSection()
            {
                TraceWriter = new System.Collections.Generic.List<TraceWriterConfiguration>() {
                    new TraceWriterConfiguration() {
                        Filter = System.Diagnostics.Tracing.EventLevel.Warning,
                        InitializationData = Path.Combine(logDirectory, "santedb.log"),
                        TraceWriter = typeof(RolloverTextWriterTraceWriter)
                    },
                    new TraceWriterConfiguration() {
                        Filter = System.Diagnostics.Tracing.EventLevel.Error,
                        InitializationData = "santedb",
                        TraceWriter = typeof(DebugDiagnosticsTraceWriter)
                    }
                }
            };
#else
            DiagnosticsConfigurationSection diagSection = new DiagnosticsConfigurationSection()
            {
                TraceWriter = new List<TraceWriterConfiguration>() {
                    new TraceWriterConfiguration () {
                        Filter = System.Diagnostics.Tracing.EventLevel.Informational,
                        InitializationData = Path.Combine(logDirectory, "santedb.log"),
                        TraceWriter = typeof(RolloverTextWriterTraceWriter)
                    }
                }
            };
#endif

            // Setup the tracers 
            diagSection.TraceWriter.ForEach(o => Tracer.AddWriter(Activator.CreateInstance(o.TraceWriter, o.Filter, o.InitializationData, null) as TraceWriter, o.Filter));
            configuration.Sections.Add(new FileSystemDispatcherQueueConfigurationSection()
            {
                QueuePath = Path.Combine(localDataPath, "queue"),
            });

            var backupSection = new BackupConfigurationSection()
            {
                PrivateBackupLocation = Path.Combine(localDataPath, "backup"),
                PublicBackupLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "santedb", "sdk", "backup")
            };

            configuration.Sections.Add(new AppletConfigurationSection()
            {
#if DEBUG
                AllowUnsignedApplets = true,
#else
                AllowUnsignedApplets = false,
#endif
                AppletDirectory = Path.Combine(localDataPath, "applets"),
                DefaultApplet = "org.santedb.uicore",
                DefaultSolution = String.Empty
            });
            configuration.Sections.Add(new RestClientConfigurationSection()
            {
                RestClientType = new TypeReferenceConfiguration(typeof(RestClient))
            });
            configuration.Sections.Add(new OAuthConfigurationSection()
            {
                IssuerName = upstreamConfiguration.Credentials[0].CredentialName,
                AllowClientOnlyGrant = false, // DCDR allows this 
                JwtSigningKey = "jwsdefault",
                TokenType = "bearer"
            });
            configuration.Sections.Add(new ClientConfigurationSection()
            {
                AutoUpdateApplets = true
            });
            configuration.Sections.Add(diagSection);
            configuration.Sections.Add(upstreamConfiguration);
            configuration.Sections.Add(new AuditAccountabilityConfigurationSection()
            {
                AuditFilters = new List<AuditFilterConfiguration>()
                {
                    // Audit any failure - No matter which event
                    new AuditFilterConfiguration(null, null, OutcomeIndicator.EpicFail | OutcomeIndicator.MinorFail | OutcomeIndicator.SeriousFail, true, true),
                    // Audit anything that creates, reads, or updates data
                    new AuditFilterConfiguration(ActionType.Create | ActionType.Read | ActionType.Update | ActionType.Delete, null, null, true, false),
                    // Audit any break the glass execution
                    new AuditFilterConfiguration(ActionType.Execute, EventIdentifierType.EmergencyOverrideStarted | EventIdentifierType.SecurityAlert, null, true, true)

                }
            });
            configuration.Sections.Add(new SynchronizationConfigurationSection()
            {
                Mode = SynchronizationMode.None,
                PollInterval = new TimeSpan(0, 15, 0),
                ForbidSending = new List<ResourceTypeReferenceConfiguration>()
                {
                    new ResourceTypeReferenceConfiguration(typeof(DeviceEntity)),
                    new ResourceTypeReferenceConfiguration(typeof(ApplicationEntity)),
                    new ResourceTypeReferenceConfiguration(typeof(Concept)),
                    new ResourceTypeReferenceConfiguration(typeof(ConceptSet)),
                    new ResourceTypeReferenceConfiguration(typeof(Place)),
                    new ResourceTypeReferenceConfiguration(typeof(ReferenceTerm)),
                    new ResourceTypeReferenceConfiguration(typeof(AssigningAuthority)),
                    new ResourceTypeReferenceConfiguration(typeof(UserEntity)),
                    new ResourceTypeReferenceConfiguration(typeof(SecurityUser)),
                    new ResourceTypeReferenceConfiguration(typeof(SecurityDevice)),
                    new ResourceTypeReferenceConfiguration(typeof(SecurityApplication))
                }
            });
            return configuration;
        }
    }
}
