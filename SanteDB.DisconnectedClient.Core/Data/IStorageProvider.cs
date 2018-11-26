/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: justin
 * Date: 2018-7-13
 */
using SanteDB.Core.Configuration;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using System;
using System.Collections.Generic;

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
