/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * User: fyfej
 * Date: 2023-5-19
 */
using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;

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
