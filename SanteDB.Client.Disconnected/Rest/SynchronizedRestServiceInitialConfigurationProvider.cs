using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Rest.AppService;
using SanteDB.Rest.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Rest
{
    /// <summary>
    /// Configuration provider
    /// </summary>
    public class SynchronizedRestServiceInitialConfigurationProvider : IInitialConfigurationProvider
    {

        /// <inheritdoc/>
        public int Order => Int32.MaxValue;

        /// <summary>
        /// Provide the initial configuration
        /// </summary>
        public SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration)
        {
            var restConfiguration = configuration.GetSection<RestConfigurationSection>().Services.Find(o=>o.ConfigurationName == AppServiceMessageHandler.ConfigurationName);
            if(restConfiguration != null)
            {
                restConfiguration.ServiceType = typeof(SynchronizedAppServiceBehavior);
                restConfiguration.Endpoints.ForEach(o => o.Contract = typeof(ISynchronizedAppServiceContract));
            }
            return configuration;
        }
    }
}
