using SanteDB.Client.Services;
using SanteDB.Core;
using SanteDB.Core.Applets;
using SanteDB.Core.Applets.Configuration;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Services;
using SanteDB.PakMan;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SanteDB.Client.Batteries.Services
{
    /// <summary>
    /// Represents a <see cref="IAppletManagerService"/> which unpacks applet static files for faster access
    /// </summary>
    public class UnpackAppletManagerService : FileSystemAppletManagerService, IReportProgressChanged
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(UnpackAppletManagerService));
        private readonly AppletConfigurationSection m_configuration;
        private readonly IAppletHostBridgeProvider m_bridgeProvider;

        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;


        /// <summary>
        /// DI constructor
        /// </summary>
        public UnpackAppletManagerService(IConfigurationManager configurationManager, IAppletHostBridgeProvider bridgeProvider)
        {
            this.m_configuration = configurationManager.GetSection<AppletConfigurationSection>();
            this.m_bridgeProvider = bridgeProvider;
            this.m_appletCollection[String.Empty].Resolver = this.ResolveAppletAsset;
            this.m_appletCollection[String.Empty].CachePages = true;
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

            if(base.Install(package, isUpgrade, null))
            {
                this.InstallInternall(package.Unpack());
                return true;
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

        /// <summary>
        /// Unpack all the directories and needed files for the installation
        /// </summary>
        private void InstallInternall(AppletManifest manifest)
        {
            // Now export all the binary files out
            var assetDirectory = Path.Combine(this.m_configuration.AppletDirectory, "assets", manifest.Info.Id);
            try
            {
                if (!Directory.Exists(assetDirectory))
                    Directory.CreateDirectory(assetDirectory);
                else
                    Directory.Delete(assetDirectory, true);

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
    }
}
