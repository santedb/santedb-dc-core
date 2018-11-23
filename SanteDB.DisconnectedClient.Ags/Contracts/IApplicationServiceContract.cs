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
 * Date: 2018-11-23
 */
using Newtonsoft.Json.Linq;
using RestSrvr.Attributes;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Contracts
{
    /// <summary>
    /// Application service contract
    /// </summary>
    [ServiceContract(Name = "APP")]
    public interface IApplicationServiceContract
    {

        /// <summary>
        /// Gets the routes
        /// </summary>
        [Get("/routes.js")]
        Stream GetRoutes();

        /// <summary>
        /// Get the configuration
        /// </summary>
        [Get("/Configuration")]
        ConfigurationViewModel GetConfiguration();

        /// <summary>
        /// Update configuration
        /// </summary>
        [Post("/Configuration")]
        ConfigurationViewModel UpdateConfiguration(ConfigurationViewModel configuration);

        /// <summary>
        /// Get the data storage providers
        /// </summary>
        [Get("/DataProviders")]
        List<StorageProviderViewModel> GetDataStorageProviders();

        /// <summary>
        /// Get user configuration
        /// </summary>
        [Get("/Configuration/User")]
        ConfigurationViewModel GetUserConfiguration();

        /// <summary>
        /// Get subscription definitions
        /// </summary>
        [Get("/SubscriptionDefinition")]
        List<AppletSubscriptionDefinition> GetSubscriptionDefinitions();

        /// <summary>
        /// Get locale assets
        /// </summary>
        [Get("/Locale")]
        Dictionary<String, String[]> GetLocaleAssets();

        /// <summary>
        /// Save the user configuration
        /// </summary>
        [Post("/Configuration/User")]
        void SaveUserConfiguration(ConfigurationViewModel configuration);

        /// <summary>
        /// Join the realm
        /// </summary>
        [Post("/Configuration/Realm")]
        ConfigurationViewModel JoinRealm(JObject configData);

    }
}
