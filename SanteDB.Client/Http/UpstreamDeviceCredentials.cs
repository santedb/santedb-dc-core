/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using SanteDB.Client.Upstream.Management;
using SanteDB.Core;
using SanteDB.Core.Services;
using System;
using System.Net;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Http
{
    /// <summary>
    /// Pricipal based credentials
    /// </summary>
    public class UpstreamDeviceCredentials : UpstreamPrincipalCredentials
    {

        // Upstream integration service
        private static ConfiguredUpstreamRealmSettings m_upstreamConfiguration;

        static UpstreamDeviceCredentials()
        {
            ApplicationServiceContext.Current.GetService<IUpstreamManagementService>().RealmChanging += (o, e) => m_upstreamConfiguration = e.UpstreamRealmSettings as ConfiguredUpstreamRealmSettings;
        }

        /// <summary>
        /// Create new principal credentials
        /// </summary>
        public UpstreamDeviceCredentials(IPrincipal principal) : base(principal)
        {
        }

        /// <summary>
        /// Set the credentials for this
        /// </summary>
        public override void SetCredentials(HttpWebRequest webRequest)
        {
            if (m_upstreamConfiguration != null)
            {
                var headerValue = Encoding.UTF8.GetBytes($"{m_upstreamConfiguration.LocalDeviceName}:{m_upstreamConfiguration.LocalDeviceSecret}");
                webRequest.Headers.Add(HttpRequestHeader.Authorization, $"BASIC {Convert.ToBase64String(headerValue)}");
            }
            else
            {
                base.SetCredentials(webRequest);
            }
        }

    }
}
