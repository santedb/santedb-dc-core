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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
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
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Tickler;
using SanteDB.Messaging.AMI.Client;

namespace SanteDB.DisconnectedClient.Configuration
{
    /// <summary>
    /// AMI update manager
    /// </summary>
    public class AmiUpdateManager : IUpdateManager, IDaemonService
    {
        // Cached credential
        private IPrincipal m_cachedCredential;

        private bool m_checkedForUpdates;

        private readonly AppletConfigurationSection m_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().Configuration.GetSection<AppletConfigurationSection>();

        // Error tickle
        private bool m_errorTickle;

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AmiUpdateManager));

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
                    // Determine if the auto-update has already occurred?
                    var lastCheck = ApplicationContext.Current.ConfigurationManager.GetAppSetting("update.auto.lastCheck");
                    if (DateTime.TryParse(lastCheck, out DateTime lastCheckDt) && lastCheckDt.Date >= DateTime.Now.Date)
                    {
                        this.m_tracer.TraceWarning("Skipping automatic update check");
                        return;
                    }
                    else
                        ApplicationContext.Current.ConfigurationManager.SetAppSetting("update.auto.lastCheck", DateTime.Now.Date.ToString("o"));

                    this.UpdateAll();
                }
                catch (Exception ex)
                {
                    if (!this.m_errorTickle)
                    {
                        ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickle(Guid.Empty, TickleType.Danger, string.Format(Strings.locale_updateCheckFailed, ex.GetType().Name)));
                        this.m_errorTickle = true;
                    }
                    this.m_tracer.TraceError("Error checking for updates: {0}", ex.Message);
                }
                finally
                {
                    ApplicationContext.Current.SetProgress(Strings.locale_idle, 1.0f);
                }
                this.m_checkedForUpdates = true;
            }
            else
            {
                this.m_tracer.TraceInfo("No network available, skipping update check");
            }

        }

        /// <summary>
        /// Update all apps
        /// </summary>
        public void UpdateAll()
        {
            try
            {
                ApplicationContext.Current.SetProgress(Strings.locale_updateCheck, 0.5f);

                // Check for new applications
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);
                amiClient.Client.Description.Endpoint[0].Timeout = 30000;
                if (amiClient.Ping())
                {
                    var solution = this.m_configuration.AppletSolution;
                    IEnumerable<AppletManifestInfo> infos = null;
                    if (!string.IsNullOrEmpty(solution))
                    {
                        infos = amiClient.Client.Get<AmiCollection>($"AppletSolution/{solution}/applet").CollectionItem.OfType<AppletManifestInfo>();
                    }
                    else
                    {
                        infos = amiClient.GetApplets().CollectionItem.OfType<AppletManifestInfo>();
                    }

                    amiClient.Client.Description.Endpoint[0].Timeout = 30000;
                    List<AppletManifestInfo> toInstall = new List<AppletManifestInfo>();
                    foreach (var i in infos)
                    {
                        var installed = ApplicationContext.Current.GetService<IAppletManagerService>().GetApplet(i.AppletInfo.Id);
                        if ((installed == null ||
                            new Version(installed.Info.Version) < new Version(i.AppletInfo.Version) &&
                            this.m_configuration.AutoUpdateApplets))
                            toInstall.Add(i);
                    }

                    if (toInstall.Count > 0 && ApplicationContext.Current.Confirm(string.Format(Strings.locale_upgradeConfirm, String.Join(",", toInstall.Select(o => o.AppletInfo.GetName("en", true))))))
                        foreach (var i in toInstall)
                            this.Install(i.AppletInfo.Id);
                }
            }
            catch (Exception ex)
            {
                if (!this.m_errorTickle)
                {
                    ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickle(Guid.Empty, TickleType.Danger, string.Format(Strings.locale_updateCheckFailed, ex.GetType().Name)));
                    this.m_errorTickle = true;
                }
                this.m_tracer.TraceError("Error checking for updates: {0}", ex.Message);
            }
            finally
            {
                ApplicationContext.Current.SetProgress(Strings.locale_idle, 1.0f);
            }
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

                    if (string.IsNullOrEmpty(this.m_configuration.AppletSolution))
                    {
                        return amiClient.StatUpdate(packageId);
                    }

                    var headers = amiClient.Client.Head($"AppletSolution/{this.m_configuration.AppletSolution}/applet/{packageId}");
                    headers.TryGetValue("X-SanteDB-PakID", out string packId);
                    headers.TryGetValue("ETag", out string versionKey);

                    return new AppletInfo
                    {
                        Id = packageId,
                        Version = versionKey
                    };
                }

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
                    amiClient.Client.ProgressChanged += (o, e) => ApplicationContext.Current.SetProgress(string.Format(Strings.locale_downloading, packageId), e.Progress);
                    amiClient.Client.Description.Endpoint[0].Timeout = 30000;

                    // Fetch the applet package
                    if (string.IsNullOrEmpty(this.m_configuration.AppletSolution))
                    {
                        using (var ms = amiClient.DownloadApplet(packageId))
                        {
                            var package = AppletPackage.Load(ms);
                            this.m_tracer.TraceInfo("Upgrading {0}...", package.Meta.ToString());
                            ApplicationContext.Current.GetService<IAppletManagerService>().Install(package, true);
                            ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickle(Guid.Empty, TickleType.Information, string.Format(Strings.locale_updateInstalled, package.Meta.Id, package.Meta.Version)));

                            // ApplicationContext.Current.Exit(); // restart
                        }
                    }
                    else
                    {
                        using (var ms = new MemoryStream(amiClient.Client.Get($"AppletSolution/{this.m_configuration.AppletSolution}/applet/{packageId}")))
                        {
                            var package = AppletPackage.Load(ms);
                            this.m_tracer.TraceInfo("Upgrading {0}...", package.Meta.ToString());
                            ApplicationContext.Current.GetService<IAppletManagerService>().Install(package, true);
                            ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickle(Guid.Empty, TickleType.Information, string.Format(Strings.locale_updateInstalled, package.Meta.Id, package.Meta.Version)));

                            // ApplicationContext.Current.Exit(); // restart
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error contacting AMI: {0}", ex.Message);
                throw new InvalidOperationException(Strings.err_updateFailed);
            }
        }

        /// <summary>
        /// True if running
        /// </summary>
        public bool IsRunning => true;

        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "AMI Update Manager";

        /// <summary>
        /// Application startup
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            if (!this.m_checkedForUpdates &&
                ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().AutoUpdateApplets)
            {
                this.AutoUpdate();
            }

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public event EventHandler Started;

        // Start events
        public event EventHandler Starting;

        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.Stopped?.Invoke(this, EventArgs.Empty);

            return true;
        }

        public event EventHandler Stopped;
        public event EventHandler Stopping;

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
    }
}
