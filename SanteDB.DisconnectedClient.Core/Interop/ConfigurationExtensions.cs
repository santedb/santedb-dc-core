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
 * Date: 2018-6-28
 */
using SanteDB.Core.Configuration;
using SanteDB.Core.Http;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using System;

namespace SanteDB.DisconnectedClient.Core.Interop
{
    /// <summary>
    /// Configuration sections
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Gets the service description.
        /// </summary>
        /// <returns>The service description.</returns>
        /// <param name="me">Me.</param>
        public static ServiceClientDescription GetServiceDescription(this SanteDBConfiguration me, String clientName)
        {

            var configSection = me.GetSection<ServiceClientConfigurationSection>();
            return configSection.Client.Find(o => clientName == o.Name)?.Clone();

        }

        /// <summary>
        /// Gets the rest client.
        /// </summary>
        /// <returns>The rest client.</returns>
        /// <param name="me">Me.</param>
        /// <param name="clientName">Client name.</param>
        public static IRestClient GetRestClient(this ApplicationContext me, String clientName)
        {
            var configSection = me.Configuration.GetSection<ServiceClientConfigurationSection>();
            var description = me.Configuration.GetServiceDescription(clientName);
            if (description == null)
                return null;

            IRestClient client = Activator.CreateInstance(configSection.RestClientType, description) as IRestClient;
            return client;
        }
    }
}
