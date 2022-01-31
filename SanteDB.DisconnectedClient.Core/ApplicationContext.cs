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
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Configuration;

//using SanteDB.DisconnectedClient.Data;
using SanteDB.DisconnectedClient.Security;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient
{
    /// <summary>
    /// Represents event arguments for progress
    /// </summary>
    public class ApplicationProgressEventArgs : EventArgs
    {
        /// <summary>
        /// The text of the progress
        /// </summary>
        public String ProgressText { get; set; }

        /// <summary>
        /// The numerical progress
        /// </summary>
        public float Progress { get; set; }
    }

    /// <summary>
    /// Application context.
    /// </summary>
    public abstract class ApplicationContext : IServiceProvider, IApplicationServiceContext, IPolicyEnforcementService
    {
        // Tracer
        protected Tracer m_tracer = Tracer.GetTracer(typeof(ApplicationContext));

        // Execution uuid
        private static Guid s_executionUuid = Guid.NewGuid();

        // True if the services are running
        private bool m_running = false;

        // Context singleton
        private static ApplicationContext s_context;

        // configuration maanger
        private IConfigurationManager m_configManager = null;

        // Service manager
        private DependencyServiceManager m_serviceManager;

        // Lock object
        private Object m_lockObject = new object();

        // Fired when application wishes to show progress of some sort
        public static event EventHandler<ApplicationProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Shows a toast on the application context
        /// </summary>
        public abstract void ShowToast(string subject);

        /// <summary>
        /// Fired when the application is starting
        /// </summary>
        public event EventHandler Starting;

        /// <summary>
        /// Fired when the application startup has completed
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        /// Fired when the application is stopping
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        /// Fired when the application has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Sets the progress
        /// </summary>
        public void SetProgress(String text, float progress)
        {
            ProgressChanged?.Invoke(this, new ApplicationProgressEventArgs() { Progress = progress, ProgressText = text });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.ApplicationContext"/> class.
        /// </summary>
        public ApplicationContext(IConfigurationPersister configPersister)
        {
            this.m_serviceManager = new DependencyServiceManager();
            this.m_configManager = new ConfigurationManager(configPersister);
            this.ConfigurationPersister = configPersister;
            this.m_serviceManager.AddServiceProvider(configPersister);
            this.m_serviceManager.AddServiceProvider(this.m_configManager);
        }

        #region IServiceProvider implementation

        /// <summary>
        /// Gets the start time of the service
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// Gets the service.
        /// </summary>
        /// <returns>The service.</returns>
        /// <typeparam name="TService">The 1st type parameter.</typeparam>
        public TService GetService<TService>() => this.m_serviceManager.GetService<TService>();

        /// <summary>
        /// Gets the service object of the specified type.
        /// </summary>
        /// <returns>The service.</returns>
        /// <param name="serviceType">Service type.</param>
        public object GetService(Type serviceType) => this.m_serviceManager.GetService(serviceType);

        #endregion IServiceProvider implementation

        /// <summary>
        /// Confirmation dialog
        /// </summary>
        public abstract bool Confirm(String confirmText);

        /// <summary>
        /// Alert dialog
        /// </summary>
        public abstract void Alert(String alertText);

        /// <summary>
        /// Gets the configuration persister
        /// </summary>
        public IConfigurationPersister ConfigurationPersister { get; private set; }

        /// <summary>
        /// Gets the current application context
        /// </summary>
        /// <value>The current.</value>
        public static ApplicationContext Current
        {
            get { return s_context; }
            set
            {
                if (s_context == null || value == null)
                    s_context = value;
            }
        }

        /// <summary>
        /// Gets the policy information service.
        /// </summary>
        /// <value>The policy information service.</value>
        public IPolicyInformationService PolicyInformationService { get { return this.GetService(typeof(IPolicyInformationService)) as IPolicyInformationService; } }

        /// <summary>
        /// Gets user preference application
        /// </summary>
        /// TODO: Move this to a service
        public SanteDBConfiguration GetUserConfiguration(String userId)
        {
            try
            {
                var userPrefDir = this.Configuration.GetSection<ApplicationConfigurationSection>().UserPrefDir;
                if (!Directory.Exists(userPrefDir))
                    Directory.CreateDirectory(userPrefDir);

                // Now we want to load
                String configFile = Path.ChangeExtension(Path.Combine(userPrefDir, userId), "userpref");
                if (!File.Exists(configFile))
                    return new SanteDBConfiguration()
                    {
                        Sections = new System.Collections.Generic.List<object>()
                        {
                            new AppletConfigurationSection(),
                            new ApplicationConfigurationSection()
                        }
                    };
                else
                    using (var fs = File.OpenRead(configFile))
                        return SanteDBConfiguration.Load(fs);
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error getting user configuration data {0}: {1}", userId, ex);
                throw;
            }
        }

        /// <summary>
        /// Save user configuration
        /// </summary>
        /// TODO: Move this to a service
        public void SaveUserConfiguration(String userId, SanteDBConfiguration config)
        {
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            else if (config == null) throw new ArgumentNullException(nameof(config));

            // Try-catch for save
            try
            {
                var userPrefDir = this.Configuration.GetSection<ApplicationConfigurationSection>().UserPrefDir;
                if (!Directory.Exists(userPrefDir))
                    Directory.CreateDirectory(userPrefDir);

                // Now we want to load
                String configFile = Path.ChangeExtension(Path.Combine(userPrefDir, userId), "userpref");
                using (var fs = File.Create(configFile))
                    config.Save(fs);
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error saving user configuration data {0}: {1}", userId, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the policy decision service.
        /// </summary>
        /// <value>The policy decision service.</value>
        public IPolicyDecisionService PolicyDecisionService { get { return this.GetService(typeof(IPolicyDecisionService)) as IPolicyDecisionService; } }

        /// <summary>
        /// Gets the identity provider service.
        /// </summary>
        /// <value>The identity provider service.</value>
        public IIdentityProviderService IdentityProviderService { get { return this.GetService(typeof(IIdentityProviderService)) as IIdentityProviderService; } }

        /// <summary>
        /// Gets the role provider service.
        /// </summary>
        /// <value>The role provider service.</value>
        public IRoleProviderService RoleProviderService { get { return this.GetService(typeof(IRoleProviderService)) as IRoleProviderService; } }

        /// <summary>
        /// Gets the configuration manager
        /// </summary>
        public SanteDBConfiguration Configuration { get { return this.m_configManager.Configuration; } }

        /// <summary>
        /// Gets the configuration manager
        /// </summary>
        public IConfigurationManager ConfigurationManager { get { return this.m_configManager; } }

        /// <summary>
        /// Gets the application information for the currently running application.
        /// </summary>
        /// <value>The application.</value>
        public abstract SecurityApplication Application { get; }

        /// <summary>
        /// Gets the device information for the currently running device
        /// </summary>
        /// <value>The device.</value>
        public virtual SecurityDevice Device
        {
            get
            {
                // TODO: Load this from configuration
                return new SanteDB.Core.Model.Security.SecurityDevice()
                {
                    Name = this.Configuration.GetSection<SecurityConfigurationSection>().DeviceName,
                    DeviceSecret = this.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret
                };
            }
        }

        /// <summary>
        /// Gets the host type
        /// </summary>
        public virtual SanteDBHostType HostType => SanteDBHostType.Client;

        /// <summary>
        /// Gets the allowed synchronization modes
        /// </summary>
        public abstract SynchronizationMode Modes { get; }

        /// <summary>
        /// Execution UUID
        /// </summary>
        public virtual Guid ExecutionUuid { get { return s_executionUuid; } }

        /// <summary>
        /// Returns true if service is running
        /// </summary>
        public bool IsRunning => this.m_running;

        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "SanteDB Disconnected Core Context";

        /// <summary>
        /// Start the daemon services
        /// </summary>
        public virtual void Start()
        {
            // Already running
            if (this.m_running)
                return;

            this.m_tracer.TraceInfo("STAGE1: Base startup initiated...");
            this.m_serviceManager.AddServiceProvider(this);

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (o, r) =>
            {
                var asmLoc = Path.Combine(Path.GetDirectoryName(typeof(ApplicationContext).Assembly.Location), r.Name.Substring(0, r.Name.IndexOf(",")) + ".dll");
                var retVal = Assembly.ReflectionOnlyLoad(r.Name);
                if (retVal != null)
                    return retVal;
                else if (File.Exists(asmLoc))
                    return Assembly.ReflectionOnlyLoadFrom(asmLoc);
                else
                    return null;
            };

            // Force load the data providers from the directory
            try
            {
                var asmLoc = Assembly.GetEntryAssembly().Location;
                if (!String.IsNullOrEmpty(asmLoc))
                {
                    foreach (var f in Directory.GetFiles(Path.GetDirectoryName(asmLoc), "*.dll"))
                    {
                        try
                        {
                            var asmL = Assembly.ReflectionOnlyLoadFrom(f);
                            if (asmL.GetExportedTypes().Any(o => o.GetInterfaces().Any(i => i.FullName == typeof(IDataConfigurationProvider).FullName)))
                            {
                                this.m_tracer.TraceInfo("Loading {0}...", f);
                                Assembly.LoadFile(f);
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceWarning("Could not scan startup assembly location: {0}", e);
            }

            // Authenticate as system principal for startup
            this.m_tracer.TraceInfo("Loading application secret");
            // Set the application secret to the configured value
            this.Application.ApplicationSecret = this.Configuration.GetSection<SecurityConfigurationSection>().ApplicationSecret ?? this.Application.ApplicationSecret;

            //ModelSettings.SourceProvider = new EntitySource.DummyEntitySource();
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_serviceManager.Start();

            this.m_tracer.TraceInfo("STAGE 3: Broadcasting startup event to services...");
            this.m_running = true;
            this.Started?.Invoke(this, EventArgs.Empty);
            this.StartTime = DateTime.Now;

            AuditUtil.AuditApplicationStartStop(EventTypeCodes.ApplicationStart);
        }

        /// <summary>
        /// Force stop
        /// </summary>
        public virtual void Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            AuditUtil.AuditApplicationStartStop(EventTypeCodes.ApplicationStop);
            this.m_serviceManager.Stop();
            this.m_serviceManager.Dispose();
            this.m_serviceManager = null;
            this.m_configManager = null;
            this.Stopped?.Invoke(this, EventArgs.Empty);
            this.m_running = false;
            s_context = null; // tear down singleton
        }

        /// <summary>
        /// Close the application
        /// </summary>
        public abstract void Exit();

        /// <summary>
        /// Add service
        /// </summary>
        public void AddServiceProvider(Type serviceType, bool addToConfiguration)
        {
            this.m_tracer.TraceInfo("Adding service provider {0}", serviceType.FullName);
            this.m_serviceManager.AddServiceProvider(serviceType);
            ApplicationServiceContextConfigurationSection appSection = this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>();
            if (addToConfiguration && !appSection.ServiceProviders.Any(o => o.Type == serviceType))
                appSection.ServiceProviders.Add(new TypeReferenceConfiguration(serviceType));
        }

        /// <summary>
        /// Remove a service provider
        /// </summary>
        public void RemoveServiceProvider(Type serviceType, bool updateConfiguration)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            ApplicationServiceContextConfigurationSection appSection = this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>();
            this.m_serviceManager.RemoveServiceProvider(serviceType);
            if (updateConfiguration)
            {
                appSection.ServiceProviders.RemoveAll(t => t.Type == serviceType);
            }
        }

        /// <summary>
        /// Instructs the current application context to get a unique identifier that should be used for encrypting/decrypting the
        /// SanteDB databases. This should be a consistent key (i.e. generate from machine, user SID, etc.).
        /// </summary>
        public abstract byte[] GetCurrentContextSecurityKey();

        /// <summary>
        /// Demand the policy
        /// </summary>
        public void Demand(string policyId)
        {
            new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, policyId).Demand();
        }

        /// <summary>
        /// Demand policy enforcement
        /// </summary>
        public void Demand(string policyId, IPrincipal principal)
        {
            new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, policyId, principal).Demand();
        }

        /// <summary>
        /// Demand policy enforcement
        /// </summary>
        public bool SoftDemand(string policyId, IPrincipal principal)
        {
            try
            {
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, policyId, principal).Demand();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}