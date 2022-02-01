/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    /// <summary>
    /// Adds localization service 
    /// </summary>
    public class LocalizationServiceMigration : IConfigurationMigration
    {
        /// <summary>
        /// Description of the matching
        /// </summary>
        public string Description => "Adds localization service";

        /// <summary>
        /// Gets the id
        /// </summary>
        public string Id => "add-locale-config";

        /// <summary>
        /// Install the specified extension
        /// </summary>
        public bool Install()
        {

            if (ApplicationContext.Current.GetService<ILocalizationService>() == null)
            {
                ApplicationContext.Current.AddServiceProvider(typeof(AppletLocalizationService), true);
            }
            return true;
        }
    }
}
