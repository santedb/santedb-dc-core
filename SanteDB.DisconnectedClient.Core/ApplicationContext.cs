/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 * 
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
 * Date: 2017-9-1
 */
using System;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Security;
using System.Security.Principal;
using SanteDB.DisconnectedClient.Core.Services;
using System.Collections.Generic;
using System.Reflection;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Model.EntityLoader;
using System.Linq;
using SanteDB.Core.Model;
using SanteDB.Core.Http;
using SanteDB.Core.Services;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Interfaces;
using System.Diagnostics;
using SanteDB.DisconnectedClient.Core.Data;
using SanteDB.Core.Diagnostics;

namespace SanteDB.DisconnectedClient.Core
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
	public abstract class ApplicationContext : IServiceProvider, IServiceManager
	{

        // Execution uuid
        private static Guid s_executionUuid = Guid.NewGuid();

        // True if the services are running
        private bool m_running = false;

		// Context singleton
		private static ApplicationContext s_context;

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
		/// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Core.ApplicationContext"/> class.
		/// </summary>
		public ApplicationContext ()
		{
            this.ThreadDefaultPrincipal = AuthenticationContext.AnonymousPrincipal;
		}

		#region IServiceProvider implementation

		/// <summary>
		/// Gets the service.
		/// </summary>
		/// <returns>The service.</returns>
		/// <typeparam name="TService">The 1st type parameter.</typeparam>
		public TService GetService<TService>()
		{
			return (TService)this.GetService (typeof(TService));
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
        public object GetService (Type serviceType)
		{
			
            

			Object candidateService = null;
			if (!this.m_cache.TryGetValue (serviceType, out candidateService)) {
				ApplicationConfigurationSection appSection = this.Configuration.GetSection<ApplicationConfigurationSection> ();
				candidateService = this.GetServices().FirstOrDefault(o => serviceType.GetTypeInfo().IsAssignableFrom(o.GetType().GetTypeInfo()));
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
		/// Gets the current application context
		/// </summary>
		/// <value>The current.</value>
		public static ApplicationContext Current
		{
			get { return s_context; }
			set {
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
        /// Save configuration
        /// </summary>
        public abstract void SaveConfiguration();

        /// <summary>
        /// Gets user preference application
        /// </summary>
        public abstract SanteDBConfiguration GetUserConfiguration(String userId);

        /// <summary>
        /// Save user configuration
        /// </summary>
        public abstract void SaveUserConfiguration(String userId, SanteDBConfiguration config);

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
		/// Gets the configuration.
		/// </summary>
		/// <value>The configuration.</value>
		public abstract SanteDBConfiguration Configuration { get; }
		/// <summary>
		/// Gets the application information for the currently running application.
		/// </summary>
		/// <value>The application.</value>
		public abstract SecurityApplication Application { get; }
		/// <summary>
		/// Gets the device information for the currently running device
		/// </summary>
		/// <value>The device.</value>
		public abstract SecurityDevice Device { get; }
        /// <summary>
        /// Gets the operating system of the current application context
        /// </summary>
        public abstract OperatingSystemID OperatingSystem { get; }
        /// <summary>
        /// Execution UUID
        /// </summary>
        public virtual Guid ExecutionUuid { get { return s_executionUuid; } }
        /// <summary>
        /// Gets the default thread principal
        /// </summary>
        public IPrincipal ThreadDefaultPrincipal { get; protected set; }
        /// <summary>
        /// Start the daemon services
        /// </summary>
        protected virtual void Start()
        {
            // Already running
            if (this.m_running)
                return;

            // Set the application secret to the configured value
            this.Application.ApplicationSecret = this.Configuration.GetSection<SecurityConfigurationSection>().ApplicationSecret ?? this.Application.ApplicationSecret;

            this.m_running = true;

            if (!this.m_cache.ContainsKey(typeof(IServiceManager)))
                this.m_cache.Add(typeof(IServiceManager), this);
            //ModelSettings.SourceProvider = new EntitySource.DummyEntitySource();
            this.Starting?.Invoke(this, EventArgs.Empty);


            ApplicationConfigurationSection config = this.Configuration.GetSection<ApplicationConfigurationSection>();
            
            var daemons = this.GetServices().OfType<IDaemonService>();
            Tracer tracer = Tracer.GetTracer(typeof(ApplicationContext));
            var nonChangeDaemons = daemons.Distinct().ToArray();
            foreach (var d in nonChangeDaemons)
            {
                try
                {
                    tracer.TraceInfo("Starting {0}", d.GetType().Name);
                    if (!d.Start())
                        tracer.TraceWarning("{0} reported unsuccessful startup", d.GetType().Name);
                }
                catch(Exception e)
                {
                    tracer.TraceError("Daemon {0} did not start up successully!: {1}", d, e);
                    throw new TypeLoadException($"{d} failed startup: {e.Message}", e);
                }
            }

            this.GetService<IThreadPoolService>().QueueNonPooledWorkItem(o =>
            {
                this.Started?.Invoke(this, EventArgs.Empty);
            }, null);

        }

        /// <summary>
        /// Force stop
        /// </summary>
        public void Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

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
        /// Add service 
        /// </summary>
        public void AddServiceProvider(Type serviceType, bool addToConfiguration)
        {

            ApplicationConfigurationSection appSection = this.Configuration.GetSection<ApplicationConfigurationSection>();
            if(!this.GetServices().Any(o=>o.GetType() == serviceType))
                lock(this.m_lockObject)
                    this.m_providers.Add(Activator.CreateInstance(serviceType));
            if (addToConfiguration && !appSection.ServiceTypes.Any(o => Type.GetType(o) == serviceType))
                appSection.ServiceTypes.Add(serviceType.AssemblyQualifiedName);
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
                    this.m_providers = new List<object>();
                    Tracer tracer = Tracer.GetTracer(typeof(ApplicationContext));
                    foreach (var itm in this.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes)
                    {
                        Type t = Type.GetType(itm);
                        if (t == null)
                            tracer.TraceWarning("Could not find provider {0}...", itm);
                        else
                        {
                            tracer.TraceInfo("Adding service provider {0}...", t.FullName);
                            this.m_providers.Add(Activator.CreateInstance(t));
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

            ApplicationConfigurationSection appSection = this.Configuration.GetSection<ApplicationConfigurationSection>();
            if(this.GetServices().Any(o=>o.GetType() == serviceType))
                lock(this.m_lockObject)
                    this.m_providers.RemoveAll(o=>
                    {
                        if (o.GetType() == serviceType)
                        {
                            (o as IDaemonService)?.Stop();
                            (o as IDisposable)?.Dispose();
                            return true;
                        }
                        return false;
                    });
            foreach(var p in this.m_cache.Where(o => o.Value.GetType() == serviceType).ToList())
                this.m_cache.Remove(p.Key);

            if (updateConfiguration)
                appSection.ServiceTypes.RemoveAll(t => Type.GetType(t) == serviceType);
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
    }
}

