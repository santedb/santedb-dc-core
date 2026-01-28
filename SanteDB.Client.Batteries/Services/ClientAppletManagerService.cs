/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using Acornima.Ast;
using SanteDB.Client.Configuration;
using SanteDB.Client.Services;
using SanteDB.Client.UserInterface;
using SanteDB.Core;
using SanteDB.Core.Applets;
using SanteDB.Core.Applets.Configuration;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;

namespace SanteDB.Client.Batteries.Services
{
    /// <summary>
    /// Represents a <see cref="IAppletManagerService"/> which unpacks applet static files for faster access
    /// </summary>
    public class ClientAppletManagerService : IAppletManagerService, IAppletSolutionManagerService
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(ClientAppletManagerService));
        private readonly AppletConfigurationSection m_configuration;
        private readonly IAppletHostBridgeProvider m_bridgeProvider;
        private readonly SecurityConfigurationSection m_securityConfiguration;
        private readonly ClientConfigurationSection m_clientConfiguration;
        private readonly IUserInterfaceInteractionProvider m_userInterfaceInteractionProvider;
        private readonly IPlatformSecurityProvider m_platformSecurityProvider;
        private AppletCollection m_appletCollection;
        private ReadonlyAppletCollection m_readonlyAppletCollection;

        /// <summary>
        /// DI constructor
        /// </summary>
        public ClientAppletManagerService(IConfigurationManager configurationManager, IAppletHostBridgeProvider bridgeProvider, IUserInterfaceInteractionProvider userInterfaceInteractionProvider, IPlatformSecurityProvider platformSecurityProvider)
        {
            this.m_configuration = configurationManager.GetSection<AppletConfigurationSection>();
            this.m_bridgeProvider = bridgeProvider;
            this.m_securityConfiguration = configurationManager.GetSection<SecurityConfigurationSection>();
            this.m_clientConfiguration = configurationManager.GetSection<ClientConfigurationSection>();
            this.m_userInterfaceInteractionProvider = userInterfaceInteractionProvider;
            this.m_platformSecurityProvider = platformSecurityProvider;
        }

        /// <inheritdoc/>
        public ReadonlyAppletCollection Applets => this.GetOrLoadInstalledApplets();

        /// <summary>
        /// Attempt to fetch or load the installed applets
        /// </summary>
        private ReadonlyAppletCollection GetOrLoadInstalledApplets()
        {
            if (m_appletCollection == null)
            {
                this.m_appletCollection = new AppletCollection()
                {
                    Resolver = this.ResolveAppletAsset,
#if !DEBUG
                    CachePages = true
#endif
                };

                try
                {
                    // Load packages from applets/ filesystem directory
                    var appletDir = this.m_configuration.AppletDirectory;
                    if (!Path.IsPathRooted(appletDir))
                    {
                        var location = Assembly.GetEntryAssembly()?.Location ?? Assembly.GetExecutingAssembly().Location;
                        appletDir = Path.Combine(Path.GetDirectoryName(location), this.m_configuration.AppletDirectory);
                    }

                    if (!Directory.Exists(appletDir))
                    {
                        Directory.CreateDirectory(appletDir);
                        this.m_tracer.TraceWarning("Applet directory {0} doesn't exist, no applets will be loaded", appletDir);
                    }
                    else
                    {
                        this.m_tracer.TraceInfo("Scanning {0} for applets...", appletDir);

                        var appletFiles = Directory.GetFiles(appletDir, "*.manifest").ToArray();
                        int loadedApplets = 0;
                        foreach (var file in appletFiles)
                        {
                            try
                            {
                                this.m_tracer.TraceInfo("Loading {0}...", file);
                                this.m_userInterfaceInteractionProvider.SetStatus(null, $"Loading applet {Path.GetFileNameWithoutExtension(file)}", (float)loadedApplets++ / (float)appletFiles.Length);
                                using (var fs = File.OpenRead(file))
                                {
                                    var pkg = AppletManifest.Load(fs);
                                    this.m_appletCollection.Add(pkg);
                                }
                            }
                            catch (Exception ex)
                            {
                                if (this.m_userInterfaceInteractionProvider.Confirm($"Error loading {Path.GetFileName(file)}, would you like to ignore this error?"))
                                {
                                    File.Delete(file);
                                }
                                else
                                {
                                    throw new InvalidOperationException($"Error loading {Path.GetFileName(file)}", ex);
                                }
                            }
                        }
                    }
                }
                catch (SecurityException e)
                {
                    this.m_tracer.TraceEvent(EventLevel.Error, "Error loading applets: {0}", e);
                    throw new InvalidOperationException("Cannot proceed while untrusted applets are present - Run `santedb --install-certs` or install the publisher certificate into `TrustedPublishers` certificate store", e);
                }
                catch (Exception ex)
                {
                    this.m_tracer.TraceEvent(EventLevel.Error, "Error loading applets: {0}", ex);
                    throw new InvalidOperationException("Error loading installed applets - please re-install application", ex);
                }
            }
            this.m_readonlyAppletCollection = this.m_readonlyAppletCollection ?? this.m_appletCollection.AsReadonly();
            return this.m_readonlyAppletCollection;
        }

        /// <inheritdoc/>
        public string ServiceName => "SanteDB DCDR Applet Manager";

        /// <summary>
        /// Solution list
        /// </summary>
        public IEnumerable<AppletSolution> Solutions
        {
            get
            {
                yield return new AppletSolution()
                {
                    Meta = new AppletInfo()
                    {
                        Id = this.m_clientConfiguration.UiSolution,
                        Version = typeof(ClientAppletManagerService).Assembly.GetName().Version.ToString()
                    },
                };
            }
        }

        /// <inheritdoc/>
        public event EventHandler Changed;

        /// <inheritdoc/>
        public AppletManifest GetApplet(string appletId) => this.Applets.FirstOrDefault(o => o.Info.Id == appletId);

        /// <inheritdoc/>
        public byte[] GetPackage(string appletId)
        {
            // Save the applet
            if (!Directory.Exists(this.m_configuration.AppletDirectory))
            {
                throw new InvalidOperationException(ErrorMessages.NOT_INITIALIZED);
            }

            // If we have the original copy send that if not create our own (unsigned) version don't
            var pakFile = Path.Combine(this.m_configuration.AppletDirectory, $"{appletId}.manifest");
            if (File.Exists(pakFile))
            {
                return File.ReadAllBytes(pakFile);
            }
            else
            {
                var manifest = this.GetApplet(appletId);
                using (var ms = new MemoryStream())
                {
                    manifest.CreatePackage().Save(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <inheritdoc/>
        public bool Install(AppletPackage package, bool isUpgrade = false)
        {
            this.m_tracer.TraceInfo("Installing {0} isUpgrade={1}", package.Meta.Id, isUpgrade);

            // Is the package valid?
#if !DEBUG
            if (!package.VerifySignatures(this.m_configuration.AllowUnsignedApplets, this.m_platformSecurityProvider) && !this.m_userInterfaceInteractionProvider.Confirm($"Could not validate {package.Meta.Id} - it may not be from a trusted source. Install anyways?"))
            {
                throw new SecurityException($"{package.GetType().Name} {package.Meta.Id} failed validation");
            }
#endif 
            var appletPakFile = this.GetInstallationTargetFile(package.Meta.Id);
            // Copy the package file over to our directory and copy the files
            var existingApplet = this.GetApplet(package.Meta.Id);
            if (existingApplet != null && File.Exists(appletPakFile) && !isUpgrade)
            {
                throw new InvalidOperationException($"Cannot replace {package.Meta} unless upgrade is specifically specified");
            }

            // Save the manifest contents as a uncompressed resource file
            var manifest = package.Unpack();
            using (var mfst = File.Create(appletPakFile))
            {
                manifest.Save(mfst);
            }

            var retVal = this.LoadApplet(manifest);
            this.Changed?.Invoke(this, EventArgs.Empty);

            return retVal;
        }

        /// <summary>
        /// Get the installation target file
        /// </summary>
        private string GetInstallationTargetFile(String appletId)
        {

            // Applet PAK directory - 
            var appletDir = this.m_configuration.AppletDirectory;
            if (!Path.IsPathRooted(appletDir))
            {
                appletDir = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), this.m_configuration.AppletDirectory);
            }

            // Create the applet container directory
            if (!Directory.Exists(appletDir))
            {
                Directory.CreateDirectory(appletDir);
            }

            return Path.Combine(appletDir, $"{appletId}.manifest");
        }

        /// <inheritdoc/>
        public bool LoadApplet(AppletManifest applet)
        {
            if (!this.m_appletCollection.VerifyDependencies(applet.Info))
            {
                this.m_tracer.TraceWarning($"Applet {applet.Info} depends on : [{String.Join(", ", applet.Info.Dependencies.Select(o => o.ToString()))}] which are missing or incompatible");
            }

            this.m_appletCollection.Remove(applet);
            this.m_appletCollection.Add(applet);
            this.m_appletCollection.ClearCaches();
            return true;
        }

        /// <summary>
        /// Resolve asset
        /// </summary>
        public object ResolveAppletAsset(AppletAsset navigateAsset)
        {

            if (navigateAsset.MimeType.Contains("text/javascript") && navigateAsset.Name.Contains("santedb.js"))
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
                script += $"\r\n// ---- BRIDGE PROVIDER FROM : {this.m_bridgeProvider} \r\n";
                script += this.m_bridgeProvider.GetBridgeScript();
                return script;
            }

            return navigateAsset.Content;
        }

        /// <inheritdoc/>
        public bool UnInstall(string appletId)
        {
            this.m_tracer.TraceInfo("Un-installing {0}", appletId);

            // Applet check
            var applet = this.GetApplet(appletId);

            // Dependency check
            var dependencies = this.m_appletCollection.Where(o => o.Info.Dependencies.Any(d => d.Id == appletId));
            if (dependencies.Any())
            {
                throw new InvalidOperationException($"Uninstalling {applet} would break : {String.Join(", ", dependencies.Select(o => o.Info))}");
            }

            // We're good to go!
            this.m_appletCollection.Remove(applet);
            this.m_appletCollection.ClearCaches();

            // Delete the file 
            var appletPakFile = this.GetInstallationTargetFile(appletId);
            if (File.Exists(appletPakFile))
            {
                File.Delete(appletPakFile);
            }

            this.Changed?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <inheritdoc/>
        public ReadonlyAppletCollection GetApplets(string solutionId)
        {
            return this.GetOrLoadInstalledApplets();
        }

        /// <inheritdoc/>
        public bool Install(AppletSolution solution, bool isUpgrade = false)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public AppletManifest GetApplet(string solutionId, string appletId)
        {
            return this.GetApplet(appletId);
        }

        /// <inheritdoc/>
        public byte[] GetPackage(string solutionId, string appletId)
        {
            return this.GetPackage(appletId);
        }
    }
}
