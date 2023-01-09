﻿using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace SanteDB.Client.Batteries.Services
{
    /// <summary>
    /// Initial applet installation service
    /// </summary>
    internal class InitialAppletInstallationService
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(InitialAppletInstallationService));

        /// <summary>
        /// DI constructor
        /// </summary>
        public InitialAppletInstallationService(IAppletManagerService appletManagerService, IConfigurationManager configurationManager)
        {

            // If there is an "applets" folder for seeding - let's use it
            var seedDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "applets");
            if (Directory.Exists(seedDirectory) && configurationManager is InitialConfigurationManager)
            {
                foreach (var appFile in Directory.GetFiles(seedDirectory, "*.pak"))
                {
                    try
                    {
                        using (var fs = File.OpenRead(appFile))
                        {
                            var appPackage = AppletSolution.Load(fs);
                            if (appletManagerService.Install(appPackage, true))
                            {
                                this.m_tracer.TraceWarning("Could not install seed app: {0}", appFile);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Error installing {0} - {1}", appFile, e);
                    }
                }
                configurationManager.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.RemoveAll(o => o.Type == this.GetType());
            }
        }
    }
}
