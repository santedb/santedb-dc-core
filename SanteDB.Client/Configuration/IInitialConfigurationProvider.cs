/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using SanteDB.Core;
using SanteDB.Core.Configuration;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// An initial configuration provider is used by the <see cref="InitialConfigurationManager"/> to 
    /// initialize the configuration context when no configuration is available
    /// </summary>
    public interface IInitialConfigurationProvider
    {

        /// <summary>
        /// Get the ordering of this provider
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Provide the initial configuration
        /// </summary>
        /// <param name="configuration">The configuration to be provided</param>
        /// <param name="hostContextType">The type of host context which the initial configuration provider 
        /// is running wihtin</param>
        /// <returns>The provided configuration</returns>
        SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration);

    }
}
