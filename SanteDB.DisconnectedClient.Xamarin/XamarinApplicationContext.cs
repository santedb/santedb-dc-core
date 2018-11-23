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
 * User: justin
 * Date: 2018-6-28
 */
using System;
using System.Linq;
using System.Collections.Generic;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Core.Configuration;
using System.IO;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using System.Xml.Serialization;
using System.Security.Cryptography;
using SanteDB.DisconnectedClient.Core.Security;
using System.Reflection;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using SanteDB.Core.Model.DataTypes;
using SanteDB.DisconnectedClient.Core.Services;
using System.Security;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Applets;
using SanteDB.Core.Applets.Model;
using System.Security.Principal;
using SanteDB.Core.Services;
using SanteDB.Core.Protocol;
using SanteDB.Core;
using SanteDB.DisconnectedClient.Xamarin.Configuration;
using System.Security.Cryptography.X509Certificates;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Core.Data;

namespace SanteDB.DisconnectedClient.Xamarin
{
	/// <summary>
	/// Represents an application context for Xamarin Android
	/// </summary>
	public abstract class XamarinApplicationContext : ApplicationContext
	{

        // Tracer
        protected Tracer m_tracer;

		/// <summary>
		/// Gets the current application context
		/// </summary>
		/// <value>The current.</value>
		public static XamarinApplicationContext Current { get { return ApplicationContext.Current as XamarinApplicationContext; } }

        /// <summary>
        /// Gets the configuration manager
        /// </summary>
        public abstract IConfigurationManager ConfigurationManager { get; }

        /// <summary>
        /// Install protocol
        /// </summary>
        public void InstallProtocol(IClinicalProtocol pdf)
        {
            try
            {
                this.GetService<IClinicalProtocolRepositoryService>().InsertProtocol(pdf.GetProtocolData());
            }
            catch(Exception e)
            {
                this.m_tracer?.TraceError("Error installing protocol {0}: {1}", pdf.Id, e);
                throw;
            }
        }

        /// <summary>
        /// Explicitly authenticate the specified user as the domain context
        /// </summary>
        public void Authenticate(String userName, String password)
		{
			var identityService = this.GetService<IIdentityProviderService>();
            var principal = identityService.Authenticate(userName, password);
            if (principal == null)
                throw new SecurityException(Strings.err_login_invalidusername);
            AuthenticationContext.Current = new AuthenticationContext(principal);
		}

		#region implemented abstract members of ApplicationContext

	    /// <summary>
        /// Gets the device information for the currently running device
        /// </summary>
        /// <value>The device.</value>
        public override SanteDB.Core.Model.Security.SecurityDevice Device {
			get {
				// TODO: Load this from configuration
				return new SanteDB.Core.Model.Security.SecurityDevice () {
					Name = this.Configuration.GetSection<SecurityConfigurationSection>().DeviceName,
					DeviceSecret = this.Configuration.GetSection<SecurityConfigurationSection>().DeviceSecret
				};
			}
		}

        /// <summary>
        /// Loads the user configuration for the specified user key
        /// </summary>
        public override SanteDBConfiguration GetUserConfiguration(string userId)
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
        public override void SaveUserConfiguration(string userId, SanteDBConfiguration config)
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

        #endregion

        /// <summary>
        /// Load all assemblies in the same startup directory
        /// </summary>
        protected override void Start()
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (o, r) =>
            {
                var asmLoc = Path.Combine(Path.GetDirectoryName(typeof(XamarinApplicationContext).Assembly.Location), r.Name.Substring(0, r.Name.IndexOf(",")) + ".dll");
                var retVal = Assembly.ReflectionOnlyLoad(r.Name);
                if (retVal != null)
                    return retVal;
                else if (File.Exists(asmLoc))
                    return Assembly.ReflectionOnlyLoadFrom(asmLoc);
                else
                    return null;
            };

            try
            {
                var asmLoc = Assembly.GetEntryAssembly().Location;
                if(!String.IsNullOrEmpty(asmLoc))
                {
                    foreach(var f in Directory.GetFiles(Path.GetDirectoryName(asmLoc), "*.dll"))
                    {
                        try
                        {
                            var asmL = Assembly.ReflectionOnlyLoadFrom(f);
                            if (asmL.GetExportedTypes().Any(o => o.GetInterfaces().Any(i => i.FullName == typeof(IStorageProvider).FullName)))
                            {
                                this.m_tracer.TraceInfo("Loading {0}...", f);
                                Assembly.LoadFile(f);
                            }
                        }
                        catch (Exception e) { }
                    }
                }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceWarning("Could not scan startup assembly location: {0}", e);
            }

            base.Start();
        }
    }
}

