/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using Newtonsoft.Json.Linq;
using SanteDB.Client.Configuration;
using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Core.Configuration;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Client.Disconnected.Configuration
{
    /// <summary>
    /// Synchronization configuration feature
    /// </summary>
    public class SynchronizationConfigurationFeature : IClientConfigurationFeature
    {
        private readonly IConfigurationManager m_configurationManager;
        private readonly ISubscriptionRepository m_subscriptionRepository;
        private readonly SynchronizationConfigurationSection m_configuration;
        /// <summary>
        /// The syncrhonization mode setting
        /// </summary>
        public const string MODE_SETTING = "mode";
        /// <summary>
        /// The setting which controls whether the client overwrites the server
        /// </summary>
        public const string OVERWRITE_SERVER_SETTING = "overwriteServer";
        /// <summary>
        /// The polling interval setting
        /// </summary>
        public const string POLL_SETTING = "pollInterval";
        /// <summary>
        /// The setting which controls whether big bundles should be requested from the server
        /// </summary>
        public const string BIG_BUNDLES_SETTING = "bigBundles";
        /// <summary>
        /// The setting which controls the subscriptions enabled
        /// </summary>
        public const string ENABLED_SUBSCRIPTIONS_SETTING = "subscription";
        /// <summary>
        /// The setting which controls the types of objects to subscribe to
        /// </summary>
        public const string SUBSCRIBED_OBJECT_TYPE_SETTING = "subscribeType";
        /// <summary>
        /// The setting which controls the instances of objects to subscribe to
        /// </summary>
        public const string SUBSCRIBED_OBJECTS_SETTING = "subscribeTo";
        /// <summary>
        /// The setting which controls whether patches or full resources are sent to the server
        /// </summary>
        public const string USE_PATCHES_SETTING = "usePatch";
        /// <summary>
        /// The setting which controls which resources are forbidden or to be ignored for synchronization
        /// </summary>
        public const string FORBID_SYNC_SETTING = "forbidSync";

        /// <summary>
        /// DI ctor
        /// </summary>
        public SynchronizationConfigurationFeature(IConfigurationManager configurationManager, ISubscriptionRepository subscriptionRepository)
        {
            this.m_configurationManager = configurationManager;
            this.m_subscriptionRepository = subscriptionRepository;
            this.m_configuration = this.m_configurationManager.GetSection<SynchronizationConfigurationSection>();
        }

        /// <inheritdoc/>
        public int Order => 100;

        /// <inheritdoc/>
        public string Name => "sync";

        /// <inheritdoc/>
        public ConfigurationDictionary<string, object> Configuration => this.GetConfiguration();

        /// <inheritdoc/>
        public String ReadPolicy => PermissionPolicyIdentifiers.Login;

        /// <inheritdoc/>
        public String WritePolicy => PermissionPolicyIdentifiers.AccessClientAdministrativeFunction;

        /// <summary>
        /// Get configuration
        /// </summary>
        private ConfigurationDictionary<string, object> GetConfiguration() => new ConfigurationDictionary<string, object>()
            {
                { MODE_SETTING, this.m_configuration?.Mode ?? SynchronizationMode.Full },
                { OVERWRITE_SERVER_SETTING, this.m_configuration?.OverwriteServer ?? false },
                { POLL_SETTING, this.m_configuration?.PollIntervalXml ?? "PT15M" },
                { BIG_BUNDLES_SETTING, this.m_configuration?.BigBundles ?? false },
                { ENABLED_SUBSCRIPTIONS_SETTING, this.m_configuration?.Subscriptions?.ToList() },
                { SUBSCRIBED_OBJECT_TYPE_SETTING, this.m_configuration?.SubscribeToResource?.TypeXml },
                { SUBSCRIBED_OBJECTS_SETTING, this.m_configuration?.SubscribedObjects?.ToList()  },
                { USE_PATCHES_SETTING, this.m_configuration?.UsePatches ?? false },
                { FORBID_SYNC_SETTING, this.m_configuration?.ForbidSending?.Select(o => o.TypeXml).ToList() }
            };

        /// <inheritdoc/>
        public bool Configure(SanteDBConfiguration configuration, IDictionary<string, object> featureConfiguration)
        {
            var configSection = configuration.GetSection<SynchronizationConfigurationSection>();
            var appSection = configuration.GetSection<ApplicationServiceContextConfigurationSection>();

            if (configSection == null)
            {
                configSection = new SynchronizationConfigurationSection()
                {
                    SubscribedObjects = new List<Guid>(),
                    Subscriptions = new List<Guid>()
                };
                configuration.AddSection(configSection);
            }

            // Copy subscription settings over
            configSection.OverwriteServer = (bool?)featureConfiguration[OVERWRITE_SERVER_SETTING] ?? configSection.OverwriteServer;
            configSection.BigBundles = (bool?)featureConfiguration[BIG_BUNDLES_SETTING] ?? configSection.BigBundles;

            if (Enum.TryParse<SynchronizationMode>(featureConfiguration[MODE_SETTING]?.ToString(), out var syncMode))
            {
                configSection.Mode = syncMode;
            }
            configSection.PollIntervalXml = featureConfiguration[POLL_SETTING]?.ToString() ?? configSection.PollIntervalXml;
            configSection.ForbidSending = ((IEnumerable)featureConfiguration[FORBID_SYNC_SETTING])?.OfType<JToken>().Select(o => new ResourceTypeReferenceConfiguration(o.ToString())).ToList();



            switch (configSection.Mode)
            {
                case SynchronizationMode.Full:
                    // Add a subscription for the all type 
                    configSection.Subscriptions = m_subscriptionRepository.Find(o => true).Where(d => d.ClientDefinitions.Any(c => c.Mode.HasFlag(Core.Model.Subscription.SubscriptionModeType.Full))).Select(o => o.Uuid).ToList();
                    configSection.SubscribedObjects = null;
                    configSection.SubscribeToResource = null;
                    goto case SynchronizationMode.Partial;
                case SynchronizationMode.Partial:
                    if (featureConfiguration.TryGetValue(SUBSCRIBED_OBJECTS_SETTING, out var subscriptionValueRaw) && subscriptionValueRaw is JArray subscriptionValueJarray && subscriptionValueJarray.Count > 0)
                    {
                        configSection.Subscriptions = ((IEnumerable)featureConfiguration[ENABLED_SUBSCRIPTIONS_SETTING])?.OfType<JToken>().Select(o => Guid.Parse(o.ToString())).ToList();
                        configSection.SubscribedObjects = ((IEnumerable)featureConfiguration[SUBSCRIBED_OBJECTS_SETTING])?.OfType<JToken>().Select(o => Guid.Parse(o.ToString())).ToList();
                        var subType = featureConfiguration[SUBSCRIBED_OBJECT_TYPE_SETTING].ToString();
                        if(subType == "Facility")
                        {
                            subType = "Place";
                        }
                        configSection.SubscribeToResource = new ResourceTypeReferenceConfiguration(subType);
                    }

                    break;
                case SynchronizationMode.None:
                    break;
                default:
                    throw new NotSupportedException();
            }
            return true;
        }
    }
}
