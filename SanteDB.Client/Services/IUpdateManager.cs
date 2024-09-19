/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Services;
using System;

namespace SanteDB.Client.Services
{
    /// <summary>
    /// Update manager service is responsible for checking for updates and downloading / applying them
    /// </summary>
    public interface IUpdateManager : IServiceImplementation
    {

        /// <summary>
        /// Get server version of a package
        /// </summary>
        AppletInfo GetServerInfo(String packageId);

        /// <summary>
        /// Install the specified package from the server version
        /// </summary>
        void Install(String packageId);

        /// <summary>
        /// Update all apps
        /// </summary>
        /// <param name="nonInteractive">True if the update should just not prompt for installation</param>
        void Update(bool nonInteractive);
    }
}
