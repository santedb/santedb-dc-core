using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Configuration
{
    /// <summary>
    /// Represents a provider that can add additional initial settings
    /// </summary>
    public interface IInitialConfigurationProvider
    {

        /// <summary>
        /// Provide additional settings
        /// </summary>
        SanteDBConfiguration Provide(SanteDBConfiguration existing);

    }
}
