using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Exceptions
{
    /// <summary>
    /// Upstream integration exception has occurred
    /// </summary>
    public class UpstreamIntegrationException : Exception
    {

        /// <inheritdoc/>
        public UpstreamIntegrationException(String message) : base(message)
        {
        }

        /// <inheritdoc/>
        public UpstreamIntegrationException(String message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
