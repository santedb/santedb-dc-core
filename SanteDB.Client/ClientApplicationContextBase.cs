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
using SanteDB.Client.Configuration;
using SanteDB.Client.UserInterface;
using SanteDB.Core;
using SanteDB.Core.Data;
using SanteDB.Core.Data.Backup;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Providers;
using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace SanteDB.Client
{
    /// <summary>
    /// Disconnected gateway application context
    /// </summary>
    public abstract class ClientApplicationContextBase : SanteDBContextBase
    {
        /// <summary>
        /// App data directory setting
        /// </summary>
        public const string AppDataDirectorySetting = "DataDirectory";

        // The instance name
        private readonly string m_instanceName;

        /// <inheritdoc/>
        public override string ApplicationName => this.m_instanceName;

        /// <summary>
        /// Interaction provider
        /// </summary>
        protected IUserInterfaceInteractionProvider InteractionProvider => this.GetService<IUserInterfaceInteractionProvider>();

        /// <summary>
        /// Localization service
        /// </summary>
        protected ILocalizationService LocalizationService => this.GetService<ILocalizationService>();

        /// <summary>
        /// Threadpool
        /// </summary>
        protected IThreadPoolService ThreadPoolService => this.GetService<IThreadPoolService>();

        /// <summary>
        /// Service manager
        /// </summary>
        protected IServiceManager ServiceManager => this.GetService<IServiceManager>();

        /// <summary>
        /// Creates a new disconnected application context with the specified configuration provider
        /// </summary>
        protected ClientApplicationContextBase(SanteDBHostType hostEnvironment, String instanceName, IConfigurationManager configurationManager) : base(hostEnvironment, configurationManager)
        {
            this.m_instanceName = instanceName;
            EntitySource.Current = new EntitySource(new RepositoryEntitySource());
        }

        /// <summary>
        /// Monitor status and send to UI
        /// </summary>
        private void MonitorStatus(Object sender, ProgressChangedEventArgs e)
        {
            var taskIdentifier = sender.GetType().Name;

            this.InteractionProvider.SetStatus(taskIdentifier, e.State.ToString(), e.Progress);
        }

        /// <summary>
        /// Start the application context
        /// </summary>
        public override void Start()
        {
            try
            {

                // JF - Setup the service point manager
                ServicePointManager.CheckCertificateRevocationList = false;
                ServicePointManager.DefaultConnectionLimit = Environment.ProcessorCount;
                ServicePointManager.DnsRefreshTimeout = -1;
                ServicePointManager.ReusePort = true;
                
                base.DependencyServiceManager.ProgressChanged += this.MonitorStatus;
                base.DependencyServiceManager.AddServiceProvider(typeof(DefaultClientServiceFactory));
                
                base.Start();

                this.AutoRestoreEnvironment();

                base.DependencyServiceManager.ProgressChanged -= this.MonitorStatus;

                // Bind to status updates on our UI
                foreach (var irpc in base.DependencyServiceManager.GetServices().OfType<IReportProgressChanged>())
                {
                    irpc.ProgressChanged += this.MonitorStatus;
                }
                EntitySource.Current = this.DependencyServiceManager.CreateInjected<EntitySource>();

                // A component has requested a restart 
                this.ServiceManager.GetServices().OfType<IRequestRestarts>().ToList().ForEach(svc =>
                {
                    svc.RestartRequested += (o, e) =>
                    {
                        OrmProviderManager.Current.Flush();
                        ThreadPool.QueueUserWorkItem(this.OnRestartRequested, o); // USE .NET since our own threadpool will be nerfed
                    };
                });

                // Bind a logout to flush the writeback
                this.GetService<ISessionProviderService>().Abandoned += (o, e) => OrmProviderManager.Current.Flush(); // logout triggers a flush
            }
            catch (Exception ex)
            {
                throw new Exception(ErrorMessages.CANNOT_STARTUP_CONTEXT, ex);
            }
        }

        /// <summary>
        /// Perform automatic restoration of a previous environment
        /// </summary>
        /// <remarks>
        /// In order to restore a device environment - a user may create a backup on an old environment and then restore that environment on a new tablet. This method prompts the user 
        /// for the restoration password of the previous tablet.
        /// </remarks>
        protected virtual void AutoRestoreEnvironment()
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                var backupServiceManager = this.GetService<IBackupService>();
                var symmEncryption = this.GetService<ISymmetricCryptographicProvider>();
                var configurationManager = this.GetService<IConfigurationManager>();
                var uiInteraction = this.GetService<IUserInterfaceInteractionProvider>();
                if (configurationManager is InitialConfigurationManager &&
                    backupServiceManager?.HasBackup(BackupMedia.Public) == true &&
                    uiInteraction?.Confirm(UserMessages.AUTO_RESTORE_BACKUP_CONFIGURATION_PROMPT) == true)
                {
                    try
                    {
                        var backupDescriptor = backupServiceManager.GetBackupDescriptors(BackupMedia.Public).OrderByDescending(o => o.Timestamp).First();
                        string backupSecret = backupDescriptor.IsEnrypted ? uiInteraction.Prompt(UserMessages.AUTO_RESTORE_BACKUP_SECRET, true) : String.Empty;
                        backupServiceManager.Restore(BackupMedia.Public, backupDescriptor.Label, backupSecret);
                        this.OnRestartRequested(this);
                    }
                    catch (Exception e)
                    {
                        uiInteraction.Alert(e.ToHumanReadableString());
                    }
                }
               
            }

        }


        /// <summary>
        /// Stop the service host
        /// </summary>
        public override void Stop()
        {
            // Bind to status updates on our UI
            foreach (var irpc in base.DependencyServiceManager.GetServices().OfType<IReportProgressChanged>())
            {
                irpc.ProgressChanged -= this.MonitorStatus;
            }

            base.Stop();
        }

        /// <summary>
        /// A restart has been requested by a service
        /// </summary>
        /// <param name="sender">The sender of the restart request</param>
        protected abstract void OnRestartRequested(object sender);

    }
}
