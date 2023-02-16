using SanteDB.Client.Configuration;
using SanteDB.Client.UserInterface;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
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
        private readonly IUserInterfaceInteractionProvider m_userInterfaceInteraction;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(InitialAppletInstallationService));

        /// <summary>
        /// DI constructor
        /// </summary>
        public InitialAppletInstallationService(IAppletManagerService appletManagerService, IConfigurationManager configurationManager, IUserInterfaceInteractionProvider userInterfaceInteractionProvider)
        {

            this.m_userInterfaceInteraction = userInterfaceInteractionProvider;

            var assembly = Assembly.GetEntryAssembly();
            var execassembly = Assembly.GetExecutingAssembly();

            if (null == assembly)
            {

            }
            else
            {
                // If there is an "applets" folder for seeding - let's use it
                var seedDirectory = Path.Combine(Path.GetDirectoryName(assembly.Location), "applets");
                if (Directory.Exists(seedDirectory) && configurationManager is InitialConfigurationManager)
                {
                    if (appletManagerService is IReportProgressChanged irpc)
                    {
                        irpc.ProgressChanged += Irpc_ProgressChanged;
                    }
                    else
                    {
                        irpc = null;
                    }

                    bool solutionloaded = false;

                    foreach (var appFile in Directory.GetFiles(seedDirectory, "*.pak"))
                    {
                        try
                        {
                            using (var fs = File.OpenRead(appFile))
                            {
                                var appPackage = AppletPackage.Load(fs);

                                if (appPackage is AppletSolution sln)
                                {
                                    //Check if we've already loaded a solution. Multiple solutions cannot be installed on a client.
                                    if (solutionloaded)
                                    {
                                        throw new InvalidOperationException("Multiple applet solutions cannot be installed concurrently.");
                                    }

                                    solutionloaded = true;

                                    foreach (var include in sln.Include)
                                    {
                                        if (!appletManagerService.Install(include, true))
                                        {
                                            this.m_tracer.TraceWarning("Could not install include in seed app: {0}", include.Meta.Id);
                                        }
                                    }
                                }

                                if (!appletManagerService.Install(appPackage, true))
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

                    if (irpc != null)
                    {
                        irpc.ProgressChanged -= Irpc_ProgressChanged;
                    }
                }
            }
        }

        private void Irpc_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.m_userInterfaceInteraction.SetStatus(e.State, e.Progress);
        }
    }
}
