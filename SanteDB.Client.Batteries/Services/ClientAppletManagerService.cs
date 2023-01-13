using SanteDB.Client.Services;
using SanteDB.Core;
using SanteDB.Core.Applets;
using SanteDB.Core.Applets.Configuration;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using SanteDB.PakMan;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
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

        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;


        /// <summary>
        /// DI constructor
        /// </summary>
        public ClientAppletManagerService(IConfigurationManager configurationManager, IAppletHostBridgeProvider bridgeProvider) 
            : base (configurationManager)
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
            String itmPath = System.IO.Path.Combine(
                                        this.m_configuration.AppletDirectory,
                                        "assets",
                                        navigateAsset.Manifest.Info.Id,
                                        navigateAsset.Name);

            if (navigateAsset.MimeType == "text/javascript" ||
                        navigateAsset.MimeType == "text/css" ||
                        navigateAsset.MimeType == "application/json" ||
                        navigateAsset.MimeType == "text/json" ||
                        navigateAsset.MimeType == "text/xml")
            {
                var script = File.ReadAllText(itmPath);
                if (itmPath.Contains("santedb.js") || itmPath.Contains("santedb.min.js"))
                    script += this.m_bridgeProvider.GetBridgeScript();
                return script;
            }
            else
                using (MemoryStream response = new MemoryStream())
                using (var fs = File.OpenRead(itmPath))
                {
                    fs.CopyTo(response);
                    return response.ToArray();
                }
        }

        /// <inheritdoc/>
        public override bool Install(AppletPackage package, bool isUpgrade, AppletSolution owner)
        {
            if(owner != null)
            {
                throw new InvalidOperationException(ErrorMessages.SOLUTIONS_NOT_SUPPORTED);
            }

            try
            {
                if (base.Install(package, isUpgrade, null))
                {
                    this.InstallInternal(package.Unpack());
                    return true;
                }
            }
            catch(SecurityException e) when (e.Message == "Applet failed validation")
            {
                this.m_tracer.TraceError("Received error {0} trying to install the applet - will attempt to re-install from update", e);
                File.Delete(Path.Combine(this.m_configuration.AppletDirectory, package.Meta.Id + ".pak"));
            }
            return false;
        }

        /// <summary>
        /// Uninstall the assets folder
        /// </summary>
        private void UnInstallInternal(AppletManifest applet)
        {
            this.m_appletCollection[String.Empty].Remove(applet);

            if (File.Exists(Path.Combine(this.m_configuration.AppletDirectory, applet.Info.Id)))
                File.Delete(Path.Combine(this.m_configuration.AppletDirectory, applet.Info.Id));
            if (Directory.Exists(Path.Combine(this.m_configuration.AppletDirectory, "assets", applet.Info.Id)))
                Directory.Delete(Path.Combine(this.m_configuration.AppletDirectory, "assets", applet.Info.Id), true);

            AppletCollection.ClearCaches();
        }

        /// <inheritdoc/>
        public override bool UnInstall(string packageId)
        {
            var existingPackage = this.GetApplet(packageId);
            if(base.UnInstall(packageId))
            {
                this.UnInstallInternal(existingPackage);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Unpack all the directories and needed files for the installation
        /// </summary>
        private void InstallInternal(AppletManifest manifest)
        {
            // Now export all the binary files out
            var assetDirectory = Path.Combine(this.m_configuration.AppletDirectory, "assets", manifest.Info.Id);
            try
            {
                if (!Directory.Exists(assetDirectory))
                    Directory.CreateDirectory(assetDirectory);

                for (int i = 0; i < manifest.Assets.Count; i++)
                {
                    var itm = manifest.Assets[i];
                    var itmPath = Path.Combine(assetDirectory, itm.Name);
                    this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(0.1f + (float)(0.8 * (float)i / manifest.Assets.Count), String.Format(UserMessages.INSTALLING, manifest.Info.GetName("en"))));

                    // Get dir name and create
                    if (!Directory.Exists(Path.GetDirectoryName(itmPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(itmPath));

                    // Extract content
                    switch (itm.Content)
                    {
                        case byte[] bytea:
                            if (Encoding.UTF8.GetString(bytea, 0, 4) == "LZIP")
                                using (var fs = File.Create(itmPath))
                                using (var ms = new MemoryStream(PakManTool.DeCompressContent(bytea)))
                                    ms.CopyTo(fs);
                            else
                                File.WriteAllBytes(itmPath, itm.Content as byte[]);
                            itm.Content = null;
                            break;
                        case String str:
                            File.WriteAllText(itmPath, str);
                            itm.Content = null;
                            break;
                    }
                }

                // Serialize the data to disk
                using (FileStream fs = File.Create(Path.Combine(this.m_configuration.AppletDirectory, manifest.Info.Id + ".pak")))
                {
                    var mfst = manifest.CreatePackage();
                    mfst.Meta.Hash = SHA256.Create().ComputeHash(mfst.Manifest);

                    var signCert = this.m_securityConfiguration?.Signatures?.Find(o => o.KeyName == "default")?.Certificate;
                    if (signCert != null && !this.m_configuration.AllowUnsignedApplets)
                    {
                        mfst = PakManTool.SignPackage(mfst, signCert, true);
                    }
                    mfst.Save(fs);
                }
                
                this.LoadApplet(manifest);
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Error installing applet {0} : {1}", manifest.Info.ToString(), e);

                // Remove
                if (File.Exists(assetDirectory))
                {
                    File.Delete(assetDirectory);
                }

                throw;
            }
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
