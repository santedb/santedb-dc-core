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
using SanteDB.Core;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.UI.Services;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    /// <summary>
    /// Configuration migration for default pointer
    /// </summary>
    public class DefaultResourcePointerMigration : IConfigurationMigration
    {
        /// <summary>
        /// ID of the migration
        /// </summary>
        public string Id => "999-ui-resource-pointer";

        /// <summary>
        /// Get the description
        /// </summary>
        public string Description => "Adds QR Code Generation and ResourcePointer Services";

        /// <summary>
        /// Install the services
        /// </summary>
        public bool Install()
        {
            if (ApplicationServiceContext.Current.GetService<IBarcodeProviderService>() == null)
                ApplicationContext.Current.AddServiceProvider(typeof(QrBarcodeGenerator), true);
            if (ApplicationServiceContext.Current.GetService<IResourcePointerService>() == null)
                ApplicationContext.Current.AddServiceProvider(typeof(JwsResourcePointerService), true);
            return true;
        }
    }
}
