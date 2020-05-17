using System;
using System.Collections.Generic;

namespace SanteDB.DisconnectedClient.Services
{
    /// <summary>
    /// Configuration target which can receive a pushed configuration
    /// </summary>
    public interface IConfigurationTarget
    {

        /// <summary>
        /// Gets the invariant for this software (openmrs, dhis2, etc.)
        /// </summary>
        string Invariant { get; }

        /// <summary>
        /// Push configuration to the remote target
        /// </summary>
        void PushConfiguration(Uri target, String user, String password, IDictionary<String, Object> configuration);

    }
}