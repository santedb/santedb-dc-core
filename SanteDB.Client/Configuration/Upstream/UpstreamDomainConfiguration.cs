/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using Newtonsoft.Json;
using SanteDB.Core.Services;
using System.Xml.Serialization;

namespace SanteDB.Client.Configuration.Upstream
{
    /// <summary>
    /// Configuration related to an upstream domain
    /// </summary>
    [XmlType(nameof(UpstreamRealmConfiguration), Namespace = "http://santedb.org/configuration")]
    public class UpstreamRealmConfiguration
    {
        /// <summary>
        /// Creates a new target realm configuration
        /// </summary>
        public UpstreamRealmConfiguration()
        {

        }

        /// <summary>
        /// The upstream from the specified target realm settings
        /// </summary>
        public UpstreamRealmConfiguration(IUpstreamRealmSettings settings)
        {
            this.DomainName = settings.Realm.Host;
            this.PortNumber = settings.Realm.Port;
            this.UseTls = settings.Realm.Scheme == "https";
        }

        /// <summary>
        /// The name of the upstream domain name (domain.deployment.com)
        /// </summary>
        [XmlAttribute("domain"), JsonProperty("domain")]
        public string DomainName { get; set; }

        /// <summary>
        /// The port of the domain
        /// </summary>
        [XmlAttribute("port"), JsonProperty("port")]
        public int PortNumber { get; set; }

        /// <summary>
        /// Use TLS on this connection
        /// </summary>
        [XmlAttribute("tls"), JsonProperty("tls")]
        public bool UseTls { get; set; }
    }
}