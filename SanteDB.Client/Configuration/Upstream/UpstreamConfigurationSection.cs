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
 * User: fyfej
 * Date: 2023-6-21
 */
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SanteDB.Client.Configuration.Upstream
{
    /// <summary>
    /// Upstream configuration section controls the configuration of this instance of SanteDB and information on the upstream 
    /// service
    /// </summary>
    [XmlType(nameof(UpstreamConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class UpstreamConfigurationSection : IEncryptedConfigurationSection
    {

        /// <summary>
        /// The upstream domain configuration information
        /// </summary>
        [XmlElement("realm"), JsonProperty("realm")]
        public UpstreamRealmConfiguration Realm { get; set; }

        /// <summary>
        /// Credentials that are used to contact the upstream
        /// </summary>
        [XmlElement("credentials"), JsonProperty("credentials")]
        public List<UpstreamCredentialConfiguration> Credentials { get; set; }

    }

}
