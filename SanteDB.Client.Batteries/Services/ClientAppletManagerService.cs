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
 * Date: 2023-5-19
 */
using SanteDB.Client.Services;
using SanteDB.Core.Applets;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using System;
using System.IO;
using System.Security;
using System.Text;

namespace SanteDB.Client.Batteries.Services
{
    /// <summary>
    /// Represents a <see cref="IAppletManagerService"/> which unpacks applet static files for faster access
    /// </summary>
    public class ClientAppletManagerService : FileSystemAppletManagerService, IReportProgressChanged
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(ClientAppletManagerService));
        private readonly IAppletHostBridgeProvider m_bridgeProvider;
        private readonly SecurityConfigurationSection m_securityConfiguration;

#pragma warning disable CS0067
        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;
#pragma warning restore

        /// <summary>
        /// DI constructor
        /// </summary>
        public ClientAppletManagerService(IConfigurationManager configurationManager, IAppletHostBridgeProvider bridgeProvider)
            : base(configurationManager)
        {
            this.m_bridgeProvider = bridgeProvider;
            this.m_appletCollection[String.Empty].Resolver = this.ResolveAppletAsset;
            this.m_appletCollection[String.Empty].CachePages = true;
            this.m_securityConfiguration = configurationManager.GetSection<SecurityConfigurationSection>();
        }

        /// <summary>
        /// Resolve asset
        /// </summary>
        public object ResolveAppletAsset(AppletAsset navigateAsset)
        {

            if (navigateAsset.MimeType == "text/javascript" && navigateAsset.Name.Contains("santedb.js"))
            {
                string script = String.Empty;
                switch (navigateAsset.Content)
                {
                    case String str:
                        script = str;
                        break;
                    case byte[] bytea:
                        script = Encoding.UTF8.GetString(bytea);
                        break;
                }
                script += this.m_bridgeProvider.GetBridgeScript();
                return script;
            }

            return navigateAsset.Content;
        }

        /// <inheritdoc/>
        public override bool Install(AppletPackage package, bool isUpgrade, AppletSolution owner)
        {
            if (owner != null)
            {
                throw new InvalidOperationException(ErrorMessages.SOLUTIONS_NOT_SUPPORTED);
            }

            try
            {
                return base.Install(package, isUpgrade, null);
            }
            catch (SecurityException e) when (e.Message == "Applet failed validation")
            {
                var appletPath = Path.Combine(this.m_configuration.AppletDirectory, package.Meta.Id + ".pak");
                this.m_tracer.TraceWarning("Received error {0} trying to install the applet - will attempt to re-install from update", e);

                if (File.Exists(appletPath))
                {
                    this.m_tracer.TraceError("Received error {0} trying to install the applet - will attempt to re-install from update", e);
                    File.Delete(appletPath);
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc/>
        /// <remarks>Clients can only have one applet solution</remarks>
        public override ReadonlyAppletCollection GetApplets(string solutionId) => base.GetApplets(String.Empty);

        /// <inheritdoc/>
        public override bool LoadApplet(AppletManifest applet)
        {
            if (applet.Info.Id == (this.m_configuration.DefaultApplet ?? "org.santedb.uicore"))
            {
                this.m_appletCollection[String.Empty].DefaultApplet = applet;
            }

            applet.Initialize();
            this.m_appletCollection[String.Empty].Remove(applet);
            this.m_appletCollection[String.Empty].Add(applet);
            AppletCollection.ClearCaches();
            return true;
        }
    }
}
