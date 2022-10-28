using SanteDB.Core;
using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// An initial configuration provider is used by the <see cref="InitialConfigurationManager"/> to 
    /// initialize the configuration context when no configuration is available
    /// </summary>
    public interface IInitialConfigurationProvider
    {

        /// <summary>
        /// Provide the initial configuration
        /// </summary>
        /// <param name="configuration">The configuration to be provided</param>
        /// <returns>The provided configuration</returns>
        SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration);

    }
}
