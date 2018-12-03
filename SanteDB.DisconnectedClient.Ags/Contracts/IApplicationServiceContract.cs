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
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.Core.Tickler;
using System;
using System.Collections.Generic;
using System.IO;

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

        /// <summary>
        /// Gets menus
        /// </summary>
        [Get("/Menu")]
        List<MenuInformation> GetMenus();

        /// <summary>
        /// Gets a new UUID 
        /// </summary>
        /// <remarks>TODO: Generate sequential UUIDS</remarks>
        [Get("/Uuid")]
        Guid GetUuid();

        /// <summary>
        /// Gets the tickles/reminders which are alerts in the application
        /// </summary>
        [Get("/Tickle")]
        List<Tickle> GetTickles();

        /// <summary>
        /// Creates a tickle on the service
        /// </summary>
        [Post("/Tickle")]
        void CreateTickle(Tickle data);

        /// <summary>
        /// Delete the specified tickle
        /// </summary>
        [Delete("/Tickle/{id}")]
        void DeleteTickle(Guid id);

        /// <summary>
        /// Get the health of the application
        /// </summary>
        [Get("/Health")]
        ApplicationHealthInfo GetHealth();

        /// <summary>
        /// Instruct the service to do an update
        /// </summary>
        [Post("/Update/{appId}")]
        void PerformUpdate(String appId);

        /// <summary>
        /// Gets the widgets 
        /// </summary>
        [Get("/Widgets")]
        List<AppletWidget> GetWidgets();

        /// <summary>
        /// Get a widget
        /// </summary>
        [Get("/Widgets/{widgetId}")]
        Stream GetWidget(String widgetId);
        
    }
}
