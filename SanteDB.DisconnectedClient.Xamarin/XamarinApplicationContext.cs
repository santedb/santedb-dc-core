/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using RestSrvr;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Protocol;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Xamarin.Configuration;
using SanteDB.DisconnectedClient.Xamarin.Security;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Xamarin
{
    /// <summary>
    /// Represents an application context for Xamarin Android
    /// </summary>
    public abstract class XamarinApplicationContext : ApplicationContext, IRemoteEndpointResolver, IPolicyEnforcementService

    {

        // Tracer
        protected Tracer m_tracer;

        /// <summary>
        /// Default ctor for Xamarin Application context
        /// </summary>
        public XamarinApplicationContext(IConfigurationPersister configurationPersister) : base(configurationPersister)
        {

        }

        /// <summary>
        /// Gets the current application context
        /// </summary>
        /// <value>The current.</value>
        public static new XamarinApplicationContext Current { get { return ApplicationContext.Current as XamarinApplicationContext; } }
        
        /// <summary>
        /// Install protocol
        /// </summary>
        public void InstallProtocol(IClinicalProtocol pdf)
        {
            try
            {
                this.GetService<IClinicalProtocolRepositoryService>().InsertProtocol(pdf.GetProtocolData());
            }
            catch (Exception e)
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
        public override SanteDB.Core.Model.Security.SecurityDevice Device
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

        public string ServiceName => throw new NotImplementedException();

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

            this.m_tracer.TraceInfo("Starting application context...");
            
            // ADd metadata provider
                this.AddServiceProvider(new AuditMetadataProvider());

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

            base.Start();
        }

        /// <summary>
        /// Retrieve the remote endpoint information
        /// </summary>
        /// <returns></returns>
        public string GetRemoteEndpoint()
        {
            var fwdHeader = RestOperationContext.Current?.IncomingRequest.Headers["X-Forwarded-For"];
            if (!String.IsNullOrEmpty(fwdHeader))
                return fwdHeader;
            return RestOperationContext.Current?.IncomingRequest.RemoteEndPoint.Address.ToString();
        }

        /// <summary>
        /// Get the request URL 
        /// </summary>
        public string GetRemoteRequestUrl()
        {
            return RestOperationContext.Current?.IncomingRequest.Url.ToString();
        }

        /// <summary>
        /// Demand access
        /// </summary>
        public void Demand(string policyId)
        {
            new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, policyId).Demand();
        }

        /// <summary>
        /// Demand the specified access
        /// </summary>
        public void Demand(string policyId, IPrincipal principal)
        {
            new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, policyId, principal).Demand();

        }
    }
}

