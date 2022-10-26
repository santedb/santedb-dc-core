using SanteDB.Core;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client
{
    /// <summary>
    /// Disconnected gateway application context
    /// </summary>
    public class ClientApplicationContext : SanteDBContextBase
    {

        private readonly string m_instanceName;

        /// <inheritdoc/>
        public override string ApplicationName => this.m_instanceName;

        /// <summary>
        /// Creates a new disconnected application context with the specified configuration provider
        /// </summary>
        /// <param name="hostEnvironment">The type of host environment being represented</param>
        protected ClientApplicationContext(SanteDBHostType hostEnvironment, String instanceName, IConfigurationManager configurationManager) : base(hostEnvironment, configurationManager)
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
