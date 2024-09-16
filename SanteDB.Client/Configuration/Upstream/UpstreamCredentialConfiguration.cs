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
using SanteDB.Core.Security.Configuration;
using System.Xml.Serialization;

namespace SanteDB.Client.Configuration.Upstream
{

    /// <summary>
    /// Represents an indicator of how the upstream credential should be conveyed
    /// </summary>
    [XmlType(nameof(UpstreamCredentialConveyance), Namespace = "http://santedb.org/configuration")]
    public enum UpstreamCredentialConveyance
    {
        /// <summary>
        /// Use the authentication as a certificate
        /// </summary>
        [XmlEnum("cert")]
        ClientCertificate,
        /// <summary>
        /// Use the authentication credential in a header
        /// </summary>
        [XmlEnum("header")]
        Header,
        /// <summary>
        /// Use the authentication credential as a client_id and client_secret
        /// </summary>
        [XmlEnum("secret")]
        Secret,
    }

    /// <summary>
    /// Type of credential
    /// </summary>
    [XmlType(nameof(UpstreamCredentialType), Namespace = "http://santedb.org/configuration")]
    public enum UpstreamCredentialType
    {
        /// <summary>
        /// Is an application credential
        /// </summary>
        [XmlEnum("app")]
        Application,
        /// <summary>
        /// Is a device credential
        /// </summary>
        [XmlEnum("dev")]
        Device,
        /// <summary>
        /// Is a user credential
        /// </summary>
        [XmlEnum("usr")]
        User
    }

    /// <summary>
    /// Represents a single upstream credential
    /// </summary>
    [XmlType(nameof(UpstreamCredentialConfiguration), Namespace = "http://santedb.org/configuration")]
    public class UpstreamCredentialConfiguration
    {

        // True if the object is being disclosed
        private bool m_forDisclosure = false;

        /// <summary>
        /// Gets or sets the type of credential
        /// </summary>
        [XmlAttribute("convey"), JsonProperty("convey")]
        public UpstreamCredentialConveyance Conveyance { get; set; }

        /// <summary>
        /// Gets or sets the type of credential
        /// </summary>
        [XmlAttribute("type"), JsonProperty("type")]
        public UpstreamCredentialType CredentialType { get; set; }

        /// <summary>
        /// Gets or sets the name of the credential
        /// </summary>
        [XmlAttribute("name"), JsonProperty("name")]
        public string CredentialName { get; set; }

        /// <summary>
        /// Gets or sets the secret if plaintext
        /// </summary>
        [XmlElement("secret"), JsonProperty("secret")]
        public string CredentialSecret { get; set; }

        /// <summary>
        /// Never convey the secret
        /// </summary>
        public bool ShouldSerializeCredentialSecret() => !this.m_forDisclosure;

        /// <summary>
        /// Gets or sets the certificate locator
        /// </summary>
        [XmlElement("certificate"), JsonProperty("certificate")]
        public X509ConfigurationElement CertificateSecret { get; set; }

        /// <summary>
        /// Get a copy of this object which is safe for disclosure
        /// </summary>
        public UpstreamCredentialConfiguration ForDisclosure()
        {
            var retVal = this.MemberwiseClone() as UpstreamCredentialConfiguration;
            retVal.m_forDisclosure = true;
            return retVal;
        }
    }
}