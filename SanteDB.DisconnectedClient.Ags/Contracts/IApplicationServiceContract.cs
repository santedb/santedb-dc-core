/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using Newtonsoft.Json.Linq;
using RestSrvr.Attributes;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Configuration;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Patch;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.DisconnectedClient.Tickler;
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
        /// Update configuration
        /// </summary>
        [Get("/Configuration/{scope}/setting/{keyMatch}")]
        List<AppSettingKeyValuePair> GetAppSetting(String scope, String keyMatch);

        /// <summary>
        /// Update configuration
        /// </summary>
        [Post("/Configuration/{scope}/setting")]
        ConfigurationViewModel SetAppSetting(String scope, List<AppSettingKeyValuePair> settings);

        /// <summary>
        /// Get the data storage providers
        /// </summary>
        [Get("/DataProviders")]
        List<StorageProviderViewModel> GetDataStorageProviders();

       
        /// <summary>
        /// Get locale assets
        /// </summary>
        [Get("/Locale")]
        Dictionary<String, String[]> GetLocaleAssets();

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
        /// Instruct the service to do an update
        /// </summary>
        [Post("/Update")]
        void PerformUpdate();

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

        /// <summary>
        /// Get synchronization logs
        /// </summary>
        /// <returns></returns>
        [Get("/Sync")]
        List<ISynchronizationLogEntry> GetSynchronizationLogs();

        /// <summary>
        /// Get synchronization logs
        /// </summary>
        /// <returns></returns>
        [Post("/Sync")]
        void SynchronizeNow();

        /// <summary>
        /// Reset the queue and try sync complete
        /// </summary>
        [Delete("/Queue")]
        void ResetRetry();


        /// <summary>
        /// Reset the queue and try sync complete
        /// </summary>
        [Delete("/Queue/{resourceType}")]
        void ResetRetry(String resourceType);

        /// <summary>
        /// Force requeue of all queue
        /// </summary>
        [Post("/Queue")]
        void RetryAll();

        /// <summary>
        /// Force re-sync of all queue
        /// </summary>
        [Get("/Queue")]
        Dictionary<String, int> GetQueue();

        /// <summary>
        /// Gets the queue entries
        /// </summary>
        [Get("/Queue/{queueName}")]
        List<ISynchronizationQueueEntry> GetQueue(String queueName);

        /// <summary>
        /// Get the specific queue entry
        /// </summary>
        [Get("/Queue/{queueName}/{id}")]
        IdentifiedData GetQueueData(String queueName, int id);

        /// <summary>
        /// Gets the conflict data a patch representing the difference between the server version and the local version being 
        /// </summary>
        [Get("/Queue/dead/{id}/diff")]
        Patch GetConflict(int id);

        /// <summary>
        /// Force a retry on the conflicted queue item
        /// </summary>
        [Post("/Queue/dead/{id}")]
        void Retry(int id);

        /// <summary>
        /// Perform a patch / resolution
        /// </summary>
        [RestInvoke("PATCH", "/Queue/dead/{id}")]
        IdentifiedData ResolveConflict(int id, Patch resolution);

        /// <summary>
        /// Remove a queue item
        /// </summary>
        [Delete("/Queue/{queueName}/confirm/{id}")]
        void DeleteQueueItem(String queueName, int id);

        /// <summary>
        /// Get DCG online state
        /// </summary>
        [Get("/Online")]
        Dictionary<String, bool> GetOnlineState();

        /// <summary>
        /// Push a configuration
        /// </summary>
        [Post("/PushConfig")]
        List<String> PushConfiguration(TargetedConfigurationViewModel model);

        /// <summary>
        /// Disable the specified service
        /// </summary>
        [Delete("/Configuration/Service/{serviceType}")]
        void DisableService(String serviceType);

        /// <summary>
        /// Enable the specified service
        /// </summary>
        [Post("/Configuration/Service/{serviceType}")]
        void EnableService(String serviceType);

        /// <summary>
        /// Get all templates
        /// </summary>
        [Get("/Template")]
        List<AppletTemplateDefinition> GetTemplates();

        /// <summary>
        /// Gets the specified template identifier
        /// </summary>
        [Get("/Template/{templateId}")]
        IdentifiedData GetTemplateDefinition(String templateId);

        /// <summary>
        /// Get the view for the specified template
        /// </summary>
        [Get("/Template/{templateId}/ui/view.html")]
        void GetTemplateView(String templateId);

        /// <summary>
        /// Get the form for the specified template
        /// </summary>
        [Get("/Template/{templateId}/ui/form.html")]
        void GetTemplateForm(String templateId);


    }
}
