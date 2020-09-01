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
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
//using SanteDB.DisconnectedClient.Data;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Security;
using System;
using System.Collections.Generic;
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
    public abstract class ApplicationContext : IServiceProvider, IServiceManager, IApplicationServiceContext, IPolicyEnforcementService
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

        // Providers
        private List<Object> m_providers;

        // A cache of already found providers
        private Dictionary<Type, Object> m_cache = new Dictionary<Type, object>();

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
            this.m_configManager = new ConfigurationManager(configPersister);
            this.ConfigurationPersister = configPersister;
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
        public TService GetService<TService>()
        {
            return (TService)this.GetService(typeof(TService));
        }

        /// <summary>
        /// Performance log handler
        /// </summary>
        public abstract void PerformanceLog(string className, string methodName, string tagName, TimeSpan counter);

        /// <summary>
        /// Gets the service object of the specified type.
        /// </summary>
        /// <returns>The service.</returns>
        /// <param name="serviceType">Service type.</param>
        public object GetService(Type serviceType)
        {
            if (serviceType == null) return null;

            Object candidateService = null;
            if (!this.m_cache.TryGetValue(serviceType, out candidateService))
            {
                var serviceTypeInfo = serviceType.GetTypeInfo();
                ApplicationServiceContextConfigurationSection appSection = this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>();
                candidateService = this.GetServices().ToArray().FirstOrDefault(o => o.GetType() != null && serviceTypeInfo.IsAssignableFrom(o.GetType().GetTypeInfo()));
                // Candidate service not found? Look in configuration
                if (candidateService == null)
                {
                    lock (this.m_lockObject)
                    {
                        var cserviceType = appSection.ServiceProviders.Select(o => o.Type).FirstOrDefault(o => o != null && serviceTypeInfo.IsAssignableFrom(o.GetTypeInfo()));
                        if (cserviceType == null)
                            return null;
                        else
                            candidateService = Activator.CreateInstance(cserviceType);
                    }
                }
                if (candidateService != null)
                    lock (this.m_lockObject)
                        if (!this.m_cache.ContainsKey(serviceType))
                        {
                            this.m_cache.Add(serviceType, candidateService);
                        }
                        else candidateService = this.m_cache[serviceType];
            }
            return candidateService;
        }

        #endregion

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
        protected virtual void Start()
        {
            // Already running
            if (this.m_running)
                return;

            this.m_tracer.TraceInfo("STAGE1: Base startup initiated...");
            this.AddServiceProvider(this);
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
                        catch (Exception e) { }
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceWarning("Could not scan startup assembly location: {0}", e);
            }

            // Authenticate as system principal for startup 
            AuthenticationContext.Current = new AuthenticationContext(AuthenticationContext.SystemPrincipal);

            // Add this as a provider
            this.AddServiceProvider(this);

            this.m_tracer.TraceInfo("Loading application secret");
            // Set the application secret to the configured value
            this.Application.ApplicationSecret = this.Configuration.GetSection<SecurityConfigurationSection>().ApplicationSecret ?? this.Application.ApplicationSecret;


            if (!this.m_cache.ContainsKey(typeof(IServiceManager)))
                this.m_cache.Add(typeof(IServiceManager), this);
            //ModelSettings.SourceProvider = new EntitySource.DummyEntitySource();
            this.Starting?.Invoke(this, EventArgs.Empty);


            ApplicationConfigurationSection config = this.Configuration.GetSection<ApplicationConfigurationSection>();

            var daemons = this.GetServices().OfType<IDaemonService>();
            Tracer tracer = Tracer.GetTracer(typeof(ApplicationContext));
            var nonChangeDaemons = daemons.Distinct().ToArray();
            this.m_tracer.TraceInfo("STAGE2: Starting Daemon Services...");

            foreach (var d in nonChangeDaemons.Distinct())
            {
                try
                {
                    tracer.TraceInfo("Starting {0}", d.GetType().Name);
                    if (!d.Start())
                        tracer.TraceWarning("{0} reported unsuccessful startup", d.GetType().Name);
                }
                catch (Exception e)
                {
                    tracer.TraceError("Daemon {0} did not start up successully!: {1}", d, e);
                    throw new TypeLoadException($"{d} failed startup: {e.Message}", e);
                }
            }

            this.m_tracer.TraceInfo("STAGE 3: Broadcasting startup event to services...");
            this.m_running = true;
            this.Started?.Invoke(this, EventArgs.Empty);
            this.StartTime = DateTime.Now;


            AuditUtil.AuditApplicationStartStop(EventTypeCodes.ApplicationStart);

        }
        
        /// <summary>
        /// Force stop
        /// </summary>
        public void Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            AuditUtil.AuditApplicationStartStop(EventTypeCodes.ApplicationStop);

            ApplicationConfigurationSection config = this.Configuration.GetSection<ApplicationConfigurationSection>();
            var daemons = this.GetServices().OfType<IDaemonService>();
            Tracer tracer = Tracer.GetTracer(typeof(ApplicationContext));
            foreach (var d in daemons)
            {
                tracer.TraceInfo("Stopping {0}", d.GetType().Name);
                if (!d.Stop())
                    tracer.TraceWarning("{0} reported unsuccessful startup", d.GetType().Name);
            }

            this.Stopped?.Invoke(this, EventArgs.Empty);

            this.m_running = false;
        }

        /// <summary>
        /// Close the application
        /// </summary>
        public abstract void Exit();

        /// <summary>
        /// Adds the specified service types
        /// </summary>
        public void AddServiceProvider(object serviceInstance)
        {
            if(!this.GetServices().Any(o=>o == serviceInstance))
                lock (this.m_lockObject)
                    this.m_providers.Add(serviceInstance);
        }

        /// <summary>
        /// Remove service provider from the current runtime
        /// </summary>
        public void RemoveServiceProvider(object serviceInstance)
        {
            if(this.GetServices().Any(o=>o == serviceInstance))
                lock(this.m_lockObject)
                {
                    (serviceInstance as IDaemonService)?.Stop();
                    (serviceInstance as IDisposable)?.Dispose();
                    this.m_providers.Remove(serviceInstance);
                    foreach (var p in this.m_cache.Where(o => o.Value == serviceInstance).ToList())
                        this.m_cache.Remove(p.Key);
                }
        }   

        /// <summary>
        /// Add service 
        /// </summary>
        public void AddServiceProvider(Type serviceType, bool addToConfiguration)
        {

            this.m_tracer.TraceInfo("Adding service provider {0}", serviceType.FullName);
            ApplicationServiceContextConfigurationSection appSection = this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>();
            if (!this.GetServices().Any(o => o.GetType() == serviceType))
                lock (this.m_lockObject)
                    this.m_providers.Add(Activator.CreateInstance(serviceType));
            if (addToConfiguration && !appSection.ServiceProviders.Any(o => o.Type == serviceType))
                appSection.ServiceProviders.Add(new TypeReferenceConfiguration(serviceType));
        }

        /// <summary>
        /// Get all services
        /// </summary>
        public IEnumerable<object> GetServices()
        {
            // We have to try to get the configuration
            if (this.m_providers == null)
            {
                lock (this.m_lockObject)
                {
                    this.m_providers = new List<object>()
                    {
                        this.ConfigurationManager,
                        this.ConfigurationPersister
                    };
                    Tracer tracer = Tracer.GetTracer(typeof(ApplicationContext));
                    foreach (var itm in this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders)
                    {
                        if (itm.Type == null)
                            tracer.TraceWarning("Could not find provider {0}...", itm);
                        else
                        {
                            tracer.TraceInfo("Adding service provider {0}...", itm.TypeXml);
                            this.m_providers.Add(Activator.CreateInstance(itm.Type));
                        }
                    }
                }
            }

            return this.m_providers;
        }

        /// <summary>
        /// Remove a service provider
        /// </summary>
        public void RemoveServiceProvider(Type serviceType, bool updateConfiguration)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            ApplicationServiceContextConfigurationSection appSection = this.Configuration.GetSection<ApplicationServiceContextConfigurationSection>();
            if (this.GetServices().Any(o => o.GetType() == serviceType))
                lock (this.m_lockObject)
                    this.m_providers.RemoveAll(o =>
                    {
                        if (o.GetType() == serviceType)
                        {
                            (o as IDaemonService)?.Stop();
                            (o as IDisposable)?.Dispose();
                            return true;
                        }
                        return false;
                    });
            foreach (var p in this.m_cache.Where(o => o.Value.GetType() == serviceType).ToList())
                this.m_cache.Remove(p.Key);

            if (updateConfiguration)
                appSection.ServiceProviders.RemoveAll(t => t.Type == serviceType);
        }

        /// <summary>
        /// Instructs the current application context to get a unique identifier that should be used for encrypting/decrypting the 
        /// SanteDB databases. This should be a consistent key (i.e. generate from machine, user SID, etc.).
        /// </summary>
        public abstract byte[] GetCurrentContextSecurityKey();

        /// <summary>
        /// Add service provider 
        /// </summary>
        public void AddServiceProvider(Type serviceType)
        {
            this.AddServiceProvider(serviceType, false);
        }

        /// <summary>
        /// Remove the service provider
        /// </summary>
        public void RemoveServiceProvider(Type serviceType)
        {
            this.RemoveServiceProvider(serviceType, false);
        }

        /// <summary>
        /// Get all types
        /// </summary>
        public abstract IEnumerable<Type> GetAllTypes();


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

    }
}

