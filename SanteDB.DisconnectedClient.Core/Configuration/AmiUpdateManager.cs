﻿/*
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
using SanteDB.Core.Api.Security;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model.AMI.Applet;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Configuration
{
    /// <summary>
    /// AMI update manager
    /// </summary>
    public class AmiUpdateManager : IUpdateManager, IDaemonService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "AMI Update Manager";

        // Cached credential
        private IPrincipal m_cachedCredential = null;

        private bool m_checkedForUpdates = false;

        private AppletConfigurationSection m_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().Configuration.GetSection<AppletConfigurationSection>();

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AmiUpdateManager));

        // Error tickle
        private bool m_errorTickle = false;

        // Start events
        public event EventHandler Starting;
        public event EventHandler Started;
        public event EventHandler Stopping;
        public event EventHandler Stopped;

        /// <summary>
        /// True if running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets current credentials
        /// </summary>
        private Credentials GetCredentials(IRestClient client)
        {
            var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            AuthenticationContext.Current = new AuthenticationContext(this.m_cachedCredential ?? AuthenticationContext.Current.Principal);

            // TODO: Clean this up - Login as device account
            if (!AuthenticationContext.Current.Principal.Identity.IsAuthenticated ||
                ((AuthenticationContext.Current.Principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MinValue) < DateTime.Now)
            {
                AuthenticationContext.Current = new AuthenticationContext(ApplicationContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret));
                this.m_cachedCredential = AuthenticationContext.Current.Principal;
            }
            return client.Description.Binding.Security.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Get the server version of a package
        /// </summary>
        public AppletInfo GetServerVersion(string packageId)
        {
            try
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                {
                    var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                    amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);

                    if (String.IsNullOrEmpty(this.m_configuration.AppletSolution))
                        return amiClient.StatUpdate(packageId);
                    else
                    {
                        var headers = amiClient.Client.Head($"AppletSolution/{this.m_configuration.AppletSolution}/applet/{packageId}");
                        headers.TryGetValue("X-SanteDB-PakID", out string packId);
                        headers.TryGetValue("ETag", out string versionKey);

                        return new AppletInfo()
                        {
                            Id = packageId,
                            Version = versionKey
                        };
                    }
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error contacting AMI: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Install the specified package
        /// </summary>
        public void Install(string packageId)
        {
            try
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                {

                    var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                    amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);
                    amiClient.Client.ProgressChanged += (o, e) => ApplicationContext.Current.SetProgress(String.Format(Strings.locale_downloading, packageId), e.Progress);
                    amiClient.Client.Description.Endpoint[0].Timeout = 30000;

                    // Fetch the applet package
                    if (String.IsNullOrEmpty(this.m_configuration.AppletSolution))
                        using (var ms = amiClient.DownloadApplet(packageId))
                        {
                            var package = AppletPackage.Load(ms);
                            this.m_tracer.TraceInfo("Upgrading {0}...", package.Meta.ToString());
                            ApplicationContext.Current.GetService<IAppletManagerService>().Install(package, true);
                            ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Information, String.Format(Strings.locale_updateInstalled, package.Meta.Id, package.Meta.Version)));

                            // ApplicationContext.Current.Exit(); // restart
                        }
                    else
                    {
                        using (var ms = new MemoryStream(amiClient.Client.Get($"AppletSolution/{this.m_configuration.AppletSolution}/applet/{packageId}")))
                        {
                            var package = AppletPackage.Load(ms);
                            this.m_tracer.TraceInfo("Upgrading {0}...", package.Meta.ToString());
                            ApplicationContext.Current.GetService<IAppletManagerService>().Install(package, true);
                            ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Information, String.Format(Strings.locale_updateInstalled, package.Meta.Id, package.Meta.Version)));

                            // ApplicationContext.Current.Exit(); // restart
                        }
                    }
                }
                else
                    return;
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error contacting AMI: {0}", ex.Message);
                throw new InvalidOperationException(Strings.err_updateFailed);
            }
        }

        /// <summary>
        /// Check for updates
        /// </summary>
        public void AutoUpdate()
        {
            // Check for updates
            if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
            {
                try
                {
                    ApplicationContext.Current.SetProgress(Strings.locale_updateCheck, 0.5f);

                    // Check for new applications
                    var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                    amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);
                    amiClient.Client.Description.Endpoint[0].Timeout = 10000;
                    if (amiClient.Ping())
                    {
                        var solution = this.m_configuration.AppletSolution;
                        IEnumerable<AppletManifestInfo> infos = null;
                        if (!String.IsNullOrEmpty(solution))
                        {
                            infos = amiClient.Client.Get<AmiCollection>($"AppletSolution/{solution}/applet").CollectionItem.OfType<AppletManifestInfo>();
                        }
                        else
                            infos = amiClient.GetApplets().CollectionItem.OfType<AppletManifestInfo>();

                        amiClient.Client.Description.Endpoint[0].Timeout = 30000;
                        foreach (var i in infos)
                        {
                            var installed = ApplicationContext.Current.GetService<IAppletManagerService>().GetApplet(i.AppletInfo.Id);
                            if (installed == null ||
                                new Version(installed.Info.Version) < new Version(i.AppletInfo.Version) &&
                                this.m_configuration.AutoUpdateApplets &&
                                ApplicationContext.Current.Confirm(String.Format(Strings.locale_upgradeConfirm, i.AppletInfo.Names[0].Value, i.AppletInfo.Version, installed.Info.Version)))
                                this.Install(i.AppletInfo.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!this.m_errorTickle)
                    {
                        ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format(Strings.locale_updateCheckFailed, ex.GetType().Name)));
                        this.m_errorTickle = true;
                    }
                    this.m_tracer.TraceError("Error checking for updates: {0}", ex.Message);
                }
                this.m_checkedForUpdates = true;
            }
            else
            {
                this.m_tracer.TraceInfo("No network available, skipping update check");
            }

        }

        /// <summary>
        /// Application startup
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            if (!this.m_checkedForUpdates &&
                ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().AutoUpdateApplets)
                this.AutoUpdate();

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.Stopped?.Invoke(this, EventArgs.Empty);

            return true;
        }
    }
}
