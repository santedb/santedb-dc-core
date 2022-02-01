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
 * Date: 2021-9-23
 */
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Services.Remote;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    /// <summary>
    /// Adds the checkout manager
    /// </summary>
    public class CheckoutManagerMigration : IConfigurationMigration
    {
        /// <summary>
        /// Gets the description of the migration
        /// </summary>
        public string Description => "Ensure appropriate checkout manager registered";

        /// <summary>
        /// Gets the identifier of the migration
        /// </summary>
        public string Id => "99c-checkout-manager";

        /// <summary>
        /// Install the migration
        /// </summary>
        public bool Install()
        {
            if (ApplicationContext.Current.GetService<IResourceCheckoutService>() == null)
            {
                // Connected to sync
                var syncMode = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>()?.Mode;
                if (!syncMode.HasValue || syncMode == SynchronizationMode.Online)
                {
                    ApplicationContext.Current.AddServiceProvider(typeof(RemoteResourceCheckoutService), true);
                }
                else // user central server for checkout
                {
                    ApplicationContext.Current.AddServiceProvider(typeof(CachedResourceCheckoutService), true);
                }
            }
            return true;
        }
    }
}