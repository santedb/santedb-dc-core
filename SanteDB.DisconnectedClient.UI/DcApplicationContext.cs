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
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
//using SanteDB.DisconnectedClient.Data;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Backup;
using SanteDB.DisconnectedClient.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using SanteDB.Core.Exceptions;
using System.Xml;

namespace SanteDB.DisconnectedClient.UI
{
    /// <summary>
    /// Test application context
    /// </summary>
    public class DcApplicationContext : ApplicationContext
    {

        // Host type
        private readonly SanteDBHostType m_santeDBHostType;
        // Dialog provider
        private IDialogProvider m_dialogProvider = null;

        // XSD SanteDB
        private static readonly XNamespace xs_santedb = "http://santedb.org/applet";

        // The application
        private static SanteDB.Core.Model.Security.SecurityApplication c_application;

        // Applet bas directory
        private Dictionary<AppletManifest, String> m_appletBaseDir = new Dictionary<AppletManifest, string>();

        /// <summary>
        /// Gets the instance name
        /// </summary>
        public String InstanceName { get; private set; }

        /// <summary>
        /// Gets or sets the synchronization modes
        /// </summary>
        public override SynchronizationMode Modes => SynchronizationMode.Sync | SynchronizationMode.Online;

        /// <summary>
        /// Show toast
        /// </summary>
        public override void ShowToast(string subject)
        {

        }

        /// <summary>
        /// Gets or sets the host type
        /// </summary>
        public override SanteDBHostType HostType => this.m_santeDBHostType;

        /// <summary>
        /// Get the application
        /// </summary>
        public override SecurityApplication Application
        {
            get
            {
                return c_application;
            }
        }

        /// <summary>
        /// Static CTOR bind to global handlers to log errors
        /// </summary>
        /// <value>The current.</value>
        static DcApplicationContext()
        {

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (ApplicationContext.Current != null)
                {
                    Tracer tracer = Tracer.GetTracer(typeof(ApplicationContext));
                    tracer.TraceEvent(EventLevel.Critical, "Uncaught exception: {0}", e.ExceptionObject.ToString());
                }
            };


        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DcApplicationContext"/> class.
        /// </summary>
        /// <param name="dialogProvider">Dialog provider.</param>
        public DcApplicationContext(IDialogProvider dialogProvider, String instanceName, SecurityApplication applicationId, SanteDBHostType hostType)
            : base(new DcConfigurationManager(instanceName))
        {
            this.m_dialogProvider = dialogProvider;
            c_application = applicationId;
            this.InstanceName = instanceName;
            this.m_santeDBHostType = hostType;
        }

        /// <summary>
        /// Start a restore context
        /// </summary>
        /// <returns></returns>
        public static bool StartRestore(IDialogProvider dialogProvider, String instanceName, SecurityApplication applicationId, SanteDBHostType hostType)
        {
            try
            {
                var retVal = new DcApplicationContext(dialogProvider, instanceName, applicationId, hostType);
                retVal.SetProgress("Start restore", 0);
                //retVal.AddServiceProvider(typeof(ConfigurationManager));

                ApplicationServiceContext.Current = DcApplicationContext.Current = retVal;
                retVal.m_tracer = Tracer.GetTracer(typeof(DcApplicationContext));
                foreach (var tr in retVal.Configuration.GetSection<DiagnosticsConfigurationSection>().TraceWriter)
                    Tracer.AddWriter(Activator.CreateInstance(tr.TraceWriter, tr.Filter, tr.InitializationData) as TraceWriter, tr.Filter);
                retVal.AddServiceProvider(typeof(DefaultBackupService));
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("SanteDB FATAL: {0}", e.ToString());
                return false;
            }
        }
        /// <summary>
        /// Starts the application context using in-memory default configuration for the purposes of 
        /// configuring the software
        /// </summary>
        /// <returns><c>true</c>, if temporary was started, <c>false</c> otherwise.</returns>
        public static bool StartTemporary(IDialogProvider dialogProvider, String instanceName, SecurityApplication applicationId, SanteDBHostType hostType)
        {
            try
            {
                var retVal = new DcApplicationContext(dialogProvider, instanceName, applicationId, hostType);
                retVal.SetProgress("Run setup", 0);
                //retVal.AddServiceProvider(typeof(ConfigurationManager));

                ApplicationServiceContext.Current = DcApplicationContext.Current = retVal;
                retVal.m_tracer = Tracer.GetTracer(typeof(DcApplicationContext));
                foreach (var tr in retVal.Configuration.GetSection<DiagnosticsConfigurationSection>().TraceWriter)
                    Tracer.AddWriter(Activator.CreateInstance(tr.TraceWriter, tr.Filter, tr.InitializationData) as TraceWriter, tr.Filter);
                retVal.AddServiceProvider(typeof(DefaultBackupService));

                var appletService = retVal.GetService<IAppletManagerService>();

                retVal.SetProgress("Loading configuration", 0.2f);
                // Load all user-downloaded applets in the data directory
                foreach (var appPath in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Applets")))
                    try
                    {

                        retVal.m_tracer.TraceInfo("Installing applet {0}", appPath);
                        using (var fs = File.OpenRead(appPath))
                        {
                            AppletPackage package = AppletPackage.Load(fs);
                            appletService.Install(package, true);
                        }
                    }
                    catch (Exception e)
                    {
                        retVal.m_tracer.TraceError("Loading applet {0} failed: {1}", appPath, e.ToString());
                        throw;
                    }

                retVal.GetService<IThreadPoolService>().QueueUserWorkItem((o) => retVal.Start());

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("SanteDB FATAL: {0}", e.ToString());
                return false;
            }
        }


        /// <summary>
        /// Start the application context
        /// </summary>
        public static bool StartContext(IDialogProvider dialogProvider, String instanceName, SecurityApplication applicationId, SanteDBHostType hostType)
        {


            // Not configured
            if (!new DcConfigurationManager(instanceName).IsConfigured)
            {
                return false;
            }
            else
            {
                // Set master application context
                DcApplicationContext retVal = null;
                try
                {

                    try
                    {
                        retVal = new DcApplicationContext(dialogProvider, instanceName, applicationId, hostType);
                        ApplicationServiceContext.Current = DcApplicationContext.Current = retVal;
                        //retVal.AddServiceProvider(typeof(ConfigurationManager));
                        if (retVal.ConfigurationPersister == null)
                            throw new InvalidOperationException("Missing configuration persistence service");
                        retVal.ConfigurationPersister.Backup(retVal.Configuration);
                    }
                    catch (Exception e)
                    {
                        Trace.TraceWarning("Error loading configuration: {0}", e);
                        if (retVal.ConfigurationPersister.HasBackup() && retVal.Confirm(Strings.err_configuration_invalid_restore_prompt))
                        {
                            retVal.ConfigurationPersister.Restore();
                            retVal.ConfigurationManager.Reload();
                        }
                        else
                            throw new Exception("Could not load or backup configuration", e);
                    }

                    if(retVal.GetService<IBackupService>() == null)
                        retVal.AddServiceProvider(typeof(DefaultBackupService));

                    // Is there a backup, and if so, does the user want to restore from that backup?
                    var backupSvc = retVal.GetService<IBackupService>();
                    if (retVal.ConfigurationManager.GetAppSetting("ignore.restore") == null &&
                        backupSvc.HasBackup(BackupMedia.Public) &&
                        retVal.Confirm(Strings.locale_confirm_restore))
                    {
                        backupSvc.Restore(BackupMedia.Public);
                    }
                    else
                        backupSvc.AutoRestore();

                    // Ignore restoration
                    if (retVal.ConfigurationManager.GetAppSetting("ignore.restore") == null)
                        retVal.Configuration.GetSection<ApplicationServiceContextConfigurationSection>().AppSettings.Add(new AppSettingKeyValuePair()
                        {
                            Key = "ignore.restore",
                            Value = "true"
                        });

                    // Add tracers
                    retVal.m_tracer = Tracer.GetTracer(typeof(DcApplicationContext));
                    retVal.m_tracer.TraceInfo("Starting logging infrastructure");
                    foreach (var tr in retVal.Configuration.GetSection<DiagnosticsConfigurationSection>().TraceWriter)
                        Tracer.AddWriter(Activator.CreateInstance(tr.TraceWriter, tr.Filter, tr.InitializationData) as TraceWriter, tr.Filter);

                    retVal.SetProgress("Loading configuration", 0.2f);

                    // Load all user-downloaded applets in the data directory
                    var configuredApplets = retVal.Configuration.GetSection<AppletConfigurationSection>().Applets;

                    var appletService = retVal.GetService<IAppletManagerService>();
                    var updateService = retVal.GetService<IUpdateManager>();

                    foreach (var appletInfo in configuredApplets.ToArray())// Directory.GetFiles(this.m_configuration.GetSection<AppletConfigurationSection>().AppletDirectory)) {
                        try
                        {
                            retVal.m_tracer.TraceInfo("Loading applet {0}", appletInfo);
                            String appletPath = Path.Combine(retVal.Configuration.GetSection<AppletConfigurationSection>().AppletDirectory, appletInfo.Id);
                            using (var fs = File.OpenRead(appletPath))
                            {
                                AppletManifest manifest = AppletManifest.Load(fs);
                                // Is this applet in the allowed applets

                                // public key token match?
                                if (appletInfo.PublicKeyToken != manifest.Info.PublicKeyToken)
                                {
                                    retVal.m_tracer.TraceWarning("Applet {0} failed validation", appletInfo);
                                    ; // TODO: Raise an error
                                }

                                appletService.LoadApplet(manifest);
                            }
                        }
                        catch (Exception e)
                        {
                            if (retVal.Confirm(String.Format(Strings.err_applet_corrupt_reinstall, appletInfo.Id)))
                            {

                                String appletPath = Path.Combine(retVal.Configuration.GetSection<AppletConfigurationSection>().AppletDirectory, appletInfo.Id);
                                if (File.Exists(appletPath))
                                    File.Delete(appletPath);
                                try
                                {
                                    configuredApplets.Remove(appletInfo);
                                    updateService.Install(appletInfo.Id);
                                }
                                catch
                                {
                                    retVal.Alert(String.Format(Strings.err_updateFailed));
                                }
                            }
                            else
                            {
                                retVal.m_tracer.TraceError("Loading applet {0} failed: {1}", appletInfo, e.ToString());
                                throw new Exception($"Could not load applet {appletInfo}", e);
                            }
                        }

                    // Set the entity source
                    EntitySource.Current = new EntitySource(retVal.GetService<IEntitySourceProvider>());
                    ApplicationServiceContext.Current = ApplicationContext.Current;

                    // Ensure data migration exists
                    bool hasDatabase = retVal.ConfigurationManager.Configuration.GetSection<DcDataConfigurationSection>().ConnectionString.Count > 0;
                        try
                        {
                            // If the DB File doesn't exist we have to clear the migrations
                            if (hasDatabase && !File.Exists(retVal.ConfigurationManager.GetConnectionString(retVal.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName).GetComponent("dbfile")))
                            {
                                retVal.m_tracer.TraceWarning("Can't find the SanteDB database, will re-install all migrations");
                                retVal.Configuration.GetSection<DcDataConfigurationSection>().MigrationLog.Entry.Clear();
                            }
                            retVal.SetProgress("Migrating databases", 0.6f);

                        ConfigurationMigrator migrator = new ConfigurationMigrator();
                            migrator.Ensure(hasDatabase);


                            // Prepare clinical protocols
                            //retVal.GetService<ICarePlanService>().Repository = retVal.GetService<IClinicalProtocolRepositoryService>();

                        }
                        catch (Exception e)
                        {
                            retVal.m_tracer.TraceError(e.ToString());
                            throw new Exception("Error executing migrations", e);
                        }
                        finally
                        {
                            retVal.ConfigurationPersister.Save(retVal.Configuration);
                        }

                    // Update the applets if there are new versions
                    foreach (var appPath in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Applets")))
                        try
                        {
                            using (var fs = File.OpenRead(appPath))
                            {
                                retVal.m_tracer.TraceInfo("Checking if {0} is upgradable", appPath);
                                AppletPackage package = AppletPackage.Load(fs);
                                var existing = appletService.GetApplet(package.Meta.Id);
                                retVal.m_tracer.TraceInfo("{0} = {1} , existing = {2}", appPath, package.Meta, existing?.Info);

                                if (existing == null || new Version(existing.Info.Version) < new Version(package.Meta.Version))
                                {
                                    retVal.m_tracer.TraceInfo("Upgrading applet {0} from {1} to {2}", package.Meta.Id, existing.Info.Version, package.Meta.Version);
                                    appletService.Install(package, true);
                                }
                                
                            }
                        }
                        catch (Exception e)
                        {
                            retVal.m_tracer.TraceError("Checking upgrade for applet {0} failed: {1}", appPath, e.ToString());
                        }

                    if (retVal.GetService<IThreadPoolService>() == null)
                        throw new InvalidOperationException(("Missing thread pool service(s)"));
                    // Start daemons
                    updateService?.AutoUpdate();
                    retVal.GetService<IThreadPoolService>().QueueUserWorkItem((o) => retVal.Start());

                    //retVal.Start();

                }
                catch (Exception e)
                {
                    //ApplicationContext.Current = null;
                    AuthenticationContext.Current = new AuthenticationContext(AuthenticationContext.SystemPrincipal);
                    throw new Exception("Error starting context", e);
                }
                return true;
            }
        }

        /// <summary>
        /// Resolve the specified applet name
        /// </summary>
        private static String ResolveName(string value)
        {

            return value?.ToLower().Replace("\\", "/");
        }

        /// <summary>
        /// Exit the application
        /// </summary>
        public override void Exit()
        {
            Environment.Exit(0);
        }

        /// <summary>
        /// Confirmation
        /// </summary>
        public override bool Confirm(string confirmText)
        {
            return this.m_dialogProvider.Confirm(confirmText, String.Empty);
        }

        /// <summary>
        /// Show an alert
        /// </summary>
        public override void Alert(string alertText)
        {
            this.m_dialogProvider.Alert(alertText);
        }


        /// <summary>
        /// Performance log!
        /// </summary>
        public override void PerformanceLog(string className, string methodName, string tagName, TimeSpan counter)
        {
        }

        /// <summary>
        /// In the SanteDB DC setting the current context security key is the current windows user SID (since we're storing data in appdata it is 
        /// encrypted per user SID)
        /// </summary>
        public override byte[] GetCurrentContextSecurityKey()
        {
#if NOCRYPT
            return null;
#else
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var sid = WindowsIdentity.GetCurrent().User;
                byte[] retVal = new byte[sid.BinaryLength];
                WindowsIdentity.GetCurrent().User.GetBinaryForm(retVal, 0);
                return retVal.Concat(Encoding.UTF8.GetBytes(Environment.MachineName)).Where(o => o != 0).ToArray();
            }
            else // TODO: LINUX ENCRYPTION 
                return null;
#endif
        }

        /// <summary>
        /// Get all types
        /// </summary>
        public override IEnumerable<Type> GetAllTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).SelectMany(
                a =>
                {
                    try
                    {
                        return a.ExportedTypes;
                    }
                    catch
                    {
                        return Type.EmptyTypes;
                    }
                });
        }
    }
}
