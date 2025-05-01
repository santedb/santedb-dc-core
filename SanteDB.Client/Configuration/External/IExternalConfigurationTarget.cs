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
using System;
using System.Collections.Generic;

namespace SanteDB.Client.Configuration.External
{
    /// <summary>
    /// Configuration target which can receive a pushed configuration
    /// </summary>
    public interface IExternalConfigurationTarget
    {

        /// <summary>
        /// Gets the invariant for this software (openmrs, dhis2, etc.)
        /// </summary>
        string Invariant { get; }

        /// <summary>
        /// Push configuration to the remote target
        /// </summary>
        List<Uri> PushConfiguration(Uri target, String user, String password, IDictionary<String, Object> configuration);

    }
}