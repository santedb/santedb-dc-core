﻿/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using Org.BouncyCastle.Crypto.Agreement.JPake;
using SanteDB.Client.Configuration;
using SanteDB.Client.UserInterface;
using SanteDB.Core.Applets.Configuration;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using System;
using System.IO;
using System.Reflection;

namespace SanteDB.Client.Batteries.Services
{
    /// <summary>
    /// Initial applet installation service
    /// </summary>
    public class InitialAppletInstallationService
    {
        private readonly IUserInterfaceInteractionProvider m_userInterfaceInteraction;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(InitialAppletInstallationService));

        /// <summary>
        /// DI constructor
        /// </summary>
        public InitialAppletInstallationService(IAppletManagerService appletManagerService, IConfigurationManager configurationManager, IUserInterfaceInteractionProvider userInterfaceInteractionProvider)
        {

            // Prevent re-run even if this service hangs around
            if(!(configurationManager is InitialConfigurationManager))
            {
                return;
            }

            this.m_userInterfaceInteraction = userInterfaceInteractionProvider;

            var assembly = Assembly.GetEntryAssembly();
            var execassembly = Assembly.GetExecutingAssembly();
            string seedDirectory = null;

                
            if (null == assembly)
            {
                var appletdirectory = configurationManager.GetSection<AppletConfigurationSection>()?.AppletDirectory;

                if (!string.IsNullOrWhiteSpace(appletdirectory))
                {
                    seedDirectory = Path.Combine(Path.GetDirectoryName(appletdirectory), "pakfiles");
                }
            }
            else
            {
                // If there is an "applets" folder for seeding - let's use it
                seedDirectory = Path.Combine(Path.GetDirectoryName(assembly.Location), "applets");
            }

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
                var seedFiles = Directory.GetFiles(seedDirectory, "*.pak");
                int seedFilesInstalled = 0;
                foreach (var appFile in seedFiles)
                {
                    try
                    {
                        using (var fs = File.OpenRead(appFile))
                        {
                            
                            this.m_userInterfaceInteraction.SetStatus(null, $"Installing Applets - Don't Close the Application", (float)seedFilesInstalled++ / (float)seedFiles.Length);
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
                                        throw new InvalidOperationException($"Could not install {appPackage.Meta.Id}");
                                    }
                                }
                            }
                            if (!appletManagerService.Install(appPackage, true))
                            {
                                throw new InvalidOperationException($"Could not install {appPackage.Meta.Id}");
                            }


                        }
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Error installing {0} - {1}", appFile, e);
                        throw new InvalidOperationException("Could initialize applet environment", e);
                    }
                }
                configurationManager.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.RemoveAll(o => o.Type == this.GetType());

                if (irpc != null)
                {
                    irpc.ProgressChanged -= Irpc_ProgressChanged;
                }
            }
        }

        private void Irpc_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.m_userInterfaceInteraction.SetStatus("Applet Manager", e.State, e.Progress);
        }
    }
}
