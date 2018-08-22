using SanteDB.DisconnectedClient.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Data
{

    /// <summary>
    /// Operating systems
    /// </summary>
    public enum OperatingSystemID
    {
        Win32 = 0x1,
        Linux = 0x2,
        MacOS = 0x4,
        Android = 0x8
    }

    /// <summary>
    /// Configuration options type
    /// </summary>
    public enum ConfigurationOptionType
    {
        String,
        Boolean,
        Numeric,
        Password
    }

    /// <summary>
    /// Represents a storage provider
    /// </summary>
    public interface IStorageProvider
    {

        /// <summary>
        /// Gets the invariant name
        /// </summary>
        string Invariant { get; }

        /// <summary>
        /// Gets the name of the storage provider
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the platforms on which this storage provider works
        /// </summary>
        OperatingSystemID Platform { get; }

        /// <summary>
        /// Get the configuration options
        /// </summary>
        Dictionary<String, ConfigurationOptionType> Options { get; }

        /// <summary>
        /// Add the necessary information to the operating system configuration
        /// </summary>
        bool Configure(SanteDBConfiguration configuration, String dataDirectory, Dictionary<String, Object> options);

    }
}
