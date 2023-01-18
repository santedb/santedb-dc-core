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
using System.Xml.Linq;

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
                var script = navigateAsset.Content as string;
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
