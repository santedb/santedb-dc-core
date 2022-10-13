using SanteDB.Core;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Disconnected
{
    /// <summary>
    /// Disconnected gateway application context
    /// </summary>
    public class DisconnectedApplicationContext : SanteDBContextBase
    {

        private readonly string m_instanceName;

        /// <inheritdoc/>
        public override string ApplicationName => this.m_instanceName;

        /// <summary>
        /// Creates a new disconnected application context with the specified configuration provider
        /// </summary>
        /// <param name="hostEnvironment">The type of host environment being represented</param>
        protected DisconnectedApplicationContext(SanteDBHostType hostEnvironment, String instanceName, IConfigurationManager configurationManager) : base(hostEnvironment, configurationManager)
        {
            this.m_instanceName = instanceName;
        }

        public override void Start()
        {
            try
            {
                base.Start();
            }
            catch
            {

            }
        }

    }
}
