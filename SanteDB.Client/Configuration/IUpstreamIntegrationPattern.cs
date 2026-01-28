/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Set the defaults on any configuration that this pattern requires
        /// </summary>
        void SetDefaults(SanteDBConfiguration configuration);

    }
}
