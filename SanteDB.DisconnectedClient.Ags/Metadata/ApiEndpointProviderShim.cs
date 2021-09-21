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
using SanteDB.Core.Interop;
using System;

namespace SanteDB.DisconnectedClient.Ags.Metadata
{
    /// <summary>
    /// Allows AGS services to be discovered by the metadata exchanger
    /// </summary>
    public class ApiEndpointProviderShim : IApiEndpointProvider
    {
        /// <summary>
        /// Gets the type of API
        /// </summary>
        public ServiceEndpointType ApiType { get; }

        /// <summary>
        /// Gets the url at which this is operating
        /// </summary>
        public string[] Url { get; }

        /// <summary>
        /// Gets the capabilities of this endpoint
        /// </summary>
        public ServiceEndpointCapabilities Capabilities { get; }

        /// <summary>
        /// Gets the behavior type
        /// </summary>
        public Type BehaviorType { get; }

        /// <summary>
        /// Creates a new api endpoint behavior
        /// </summary>
        public ApiEndpointProviderShim(Type behavior, ServiceEndpointType apiType, String url, ServiceEndpointCapabilities capabilities)
        {
            this.ApiType = apiType;
            this.Url = new string[] { url };
            this.Capabilities = capabilities;
            this.BehaviorType = behavior;
        }
    }
}
