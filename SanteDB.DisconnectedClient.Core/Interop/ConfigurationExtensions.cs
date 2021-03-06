﻿/*
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
using SanteDB.Core.Configuration;
using SanteDB.Core.Http;
using SanteDB.DisconnectedClient.Configuration;
using System;

namespace SanteDB.DisconnectedClient.Interop
{
	/// <summary>
	/// Configuration sections
	/// </summary>
	public static class ConfigurationExtensions
    {
	    /// <summary>
        /// Gets the rest client.
        /// </summary>
        /// <returns>The rest client.</returns>
        /// <param name="me">Me.</param>
        /// <param name="clientName">Client name.</param>
        public static IRestClient GetRestClient(this ApplicationContext me, string clientName)
        {
            var configSection = me.Configuration.GetSection<ServiceClientConfigurationSection>();
            var description = me.Configuration.GetServiceDescription(clientName);
            if (description == null)
            {
	            return null;
            }

            var client = Activator.CreateInstance(configSection.RestClientType, description) as IRestClient;
            return client;
        }

	    /// <summary>
        /// Gets the service description.
        /// </summary>
        /// <returns>The service description.</returns>
        /// <param name="me">Me.</param>
        public static ServiceClientDescriptionConfiguration GetServiceDescription(this SanteDBConfiguration me, string clientName)
        {

            var configSection = me.GetSection<ServiceClientConfigurationSection>();
            return configSection.Client.Find(o => clientName == o.Name)?.Clone();

        }
    }
}
