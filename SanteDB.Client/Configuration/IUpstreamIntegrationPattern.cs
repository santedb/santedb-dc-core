using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// Represents an upstream integration pattern
    /// </summary>
    public interface IUpstreamIntegrationPattern
    {

        /// <summary>
        /// The name of the integration pattern
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Gets the services which should be enabled for this integration mode
        /// </summary>
        IEnumerable<Type> GetServices();


    }
}
