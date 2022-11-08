using SanteDB.Client.UserInterface;
using SanteDB.Core;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        /// <param name="hostEnvironment">The type of host environment being represented</param>
        protected ClientApplicationContextBase(SanteDBHostType hostEnvironment, String instanceName, IConfigurationManager configurationManager) : base(hostEnvironment, configurationManager)
        {
            this.m_instanceName = instanceName;
        }

        /// <summary>
        /// Start the application context
        /// </summary>
        public override void Start()
        {
            try
            {

                base.DependencyServiceManager.ProgressChanged += (o, e) => this.InteractionProvider.SetStatus(e.State.ToString(), e.Progress);

                base.Start();

                // A component has requested a restart 
                this.ServiceManager.GetServices().OfType<IRequestRestarts>().ToList().ForEach(svc =>
                {
                    svc.RestartRequested += (o, e) =>
                    {
                        ThreadPool.QueueUserWorkItem(this.OnRestartRequested, o); // USE .NET since our own threadpool will be nurfed
                    };
                });
            }
            catch
            {

            }
        }


        /// <summary>
        /// A restart has been requested by a service
        /// </summary>
        /// <param name="sender">The sender of the restart request</param>
        protected abstract void OnRestartRequested(object sender);
        
    }
}
