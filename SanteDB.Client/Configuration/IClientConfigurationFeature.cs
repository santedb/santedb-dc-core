using System;
using System.Collections.Generic;
using System.Text;
using SanteDB.Core.Configuration;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// Implementers of this class can disclose and update the <see cref="SanteDBConfiguration"/>. The 
    /// use of this class is to separate the steps of configuration with the 
    /// </summary>
    public interface IClientConfigurationFeature
    {

        /// <summary>
        /// Get the preferred order for the configuration
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets the name of the feature
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the configuration object
        /// </summary>
        ConfigurationDictionary<String, Object> Configuration { get; }

        /// <summary>
        /// Get the policy a user must have to read this configuration
        /// </summary>
        string ReadPolicy { get; }

        /// <summary>
        /// Get the policy a user must have to write this configuration
        /// </summary>
        string WritePolicy { get; }

        /// <summary>
        /// Configure this feature with the specified <paramref name="featureConfiguration"/>
        /// </summary>
        /// <param name="configuration">The configuration to which the configuration option is a target</param>
        /// <param name="featureConfiguration">The feature conifguration provided by the user</param>
        /// <returns>True if the configuraiton was successful</returns>
        bool Configure(SanteDBConfiguration configuration, IDictionary<String, Object> featureConfiguration);
    }
}
