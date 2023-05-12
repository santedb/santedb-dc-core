/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using SanteDB.Client.Configuration;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Services;
using SanteDB.Client.Tickles;
using SanteDB.Client.UserInterface;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Applet;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.IO;
using System.Linq;

namespace SanteDB.Client.Upstream
{
    /// <summary>
    /// Update manager which uses the AMI to get updates for packages
    /// </summary>
    public class UpstreamUpdateManagerService : IUpdateManager
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(UpstreamUpdateManagerService));
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IUpstreamIntegrationService m_upstreamIntegrationService;
        private readonly IUpstreamAvailabilityProvider m_upstreamAvailabilityProvider;
        private readonly ClientConfigurationSection m_configuration;
        private readonly IUserInterfaceInteractionProvider m_userInterfaceService;
        private readonly IAppletManagerService m_appletManager;
        private readonly ITickleService m_tickleService;
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Remote Applet Update Manager";

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamUpdateManagerService(IRestClientFactory restClientFactory,
            IConfigurationManager configurationManager,
            IUserInterfaceInteractionProvider userInterface,
            IAppletManagerService appletManager,
            ILocalizationService localizationService,
            ITickleService tickleService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService)
        {
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamIntegrationService = upstreamIntegrationService;
            this.m_upstreamAvailabilityProvider = upstreamAvailabilityProvider;
            this.m_configuration = configurationManager.GetSection<ClientConfigurationSection>();
            this.m_userInterfaceService = userInterface;
            this.m_appletManager = appletManager;
            this.m_tickleService = tickleService;
            this.m_localizationService = localizationService;

            if (this.m_appletManager is IDaemonService ids)
            {
                if (ids.IsRunning)
                {
                    this.AutoCheckForUpdates();
                }
                else
                {
                    ids.Started += (o, e) => this.AutoCheckForUpdates();
                }
            }
            else
            {
                this.AutoCheckForUpdates();
            }
        }

        /// <summary>
        /// Automatically check for updates
        /// </summary>
        private void AutoCheckForUpdates()
        {
            try
            {
                if (this.m_configuration.AutoUpdateApplets)
                {
                    this.Update(true);
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceWarning("Cannot check for updates - {0}", e.Message);
            }
        }

        /// <inheritdoc/>
        public AppletInfo GetServerInfo(string packageId)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId), ErrorMessages.ARGUMENT_NULL);
            }
            else if (this.m_upstreamIntegrationService == null)
            {
                throw new InvalidOperationException(ErrorMessages.UPSTREAM_NOT_CONFIGURED);

            }

            try
            {
                using (AuthenticationContext.EnterContext(this.m_upstreamIntegrationService.AuthenticateAsDevice()))
                {
                    using (var restClient = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                    {

                        var headers = restClient.Head($"AppletSolution/{this.m_configuration.UiSolution}/applet/{packageId}");
                        headers.TryGetValue("X-SanteDB-PakID", out string packId);
                        headers.TryGetValue("ETag", out string versionKey);
                        return new AppletInfo
                        {
                            Id = packageId,
                            Version = versionKey
                        };

                    }
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(String.Format(ErrorMessages.READ_ERROR, $"applet/{packageId}"), e);
            }
        }

        /// <inheritdoc/>
        public void Install(string packageId)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId), ErrorMessages.ARGUMENT_NULL);
            }
            else if (this.m_upstreamIntegrationService == null)
            {
                throw new InvalidOperationException(ErrorMessages.UPSTREAM_NOT_CONFIGURED);
            }

            try
            {
                using (AuthenticationContext.EnterContext(this.m_upstreamIntegrationService.AuthenticateAsDevice()))
                {
                    using (var restClient = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                    {

                        this.m_tracer.TraceInfo("Updating {0}...", packageId);
                        restClient.ProgressChanged += (o, e) => this.m_userInterfaceService.SetStatus("Update Manager", String.Format(UserMessages.DOWNLOADING, packageId), e.Progress);
                        restClient.SetTimeout(30000);

                        using (var ms = new MemoryStream(restClient.Get($"AppletSolution/{this.m_configuration.UiSolution}/applet/{packageId}")))
                        {
                            var package = AppletPackage.Load(ms);
                            this.m_tracer.TraceInfo("Installing {0}...", package.Meta);
                            this.m_appletManager.Install(package, true);
                            this.m_tickleService.SendTickle(new Tickle(Guid.Empty, TickleType.Information, this.m_localizationService.GetString(UserMessageStrings.UPDATE_INSTALLED, new { package = package.Meta.Id, version = package.Meta.Version })));
                        }

                    }
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(String.Format(ErrorMessages.READ_ERROR, $"applet/{packageId}"), e);

            }
        }

        /// <inheritdoc/>
        public void Update(bool nonInteractive)
        {
            if (this.m_upstreamIntegrationService == null)
            {
                this.m_tracer.TraceWarning("Upstream is not configured - skipping update");
                return;
            }

            try
            {
                if (this.m_upstreamAvailabilityProvider.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    // Are we configured to auto-update?
                    if (nonInteractive && !this.m_configuration.AutoUpdateApplets)
                    {
                        this.m_tracer.TraceWarning("Will skip checking for automatic updates...");
                        return; // Skip updates
                    }

                    this.m_tracer.TraceInfo("Checking for updates with remote service...");

                    // Set progress 
                    this.m_userInterfaceService.SetStatus("Update Manager", UserMessages.UPDATE_CHECK, 0.5f);
                    using (AuthenticationContext.EnterContext(this.m_upstreamIntegrationService.AuthenticateAsDevice()))
                    {
                        using (var restClient = this.m_restClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                        {
                            var remoteVersionInfo = restClient.Get<AmiCollection>($"AppletSolution/{this.m_configuration.UiSolution}/applet").CollectionItem
                                .OfType<AppletManifestInfo>()
                                .Where(i =>
                                {
                                    this.m_tracer.TraceVerbose("Checking for local version of {0}...", i.AppletInfo);
                                    var installed = this.m_appletManager.GetApplet(i.AppletInfo.Id);
                                    return (installed == null ||
                                        new Version(installed.Info.Version) < new Version(i.AppletInfo.Version));
                                }).ToList();

                            if (remoteVersionInfo.Any() &&
                                this.m_userInterfaceService.Confirm(String.Format(UserMessages.UPDATE_INSTALL_CONFIRM, String.Join(",", remoteVersionInfo.Select(o => o.AppletInfo.GetName("en", true))))))
                            {
                                remoteVersionInfo.ForEach(i => this.Install(i.AppletInfo.Id));
                            }


                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(String.Format(ErrorMessages.READ_ERROR, "applet"), e);
            }
        }
    }
}
