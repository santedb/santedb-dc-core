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
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Core.i18n;
using SanteDB.Core.Services;
using System;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// <see cref="IUpstreamRealmSettings"/> implementation based off the <see cref="UpstreamConfigurationSection"/>
    /// </summary>
    public class ConfiguredUpstreamRealmSettings : IUpstreamRealmSettings
    {

        /// <inheritdoc/>
        public Uri Realm { get; }

        /// <inheritdoc/>
        public string LocalDeviceName { get; }

        /// <inheritdoc/>
        public string LocalClientName { get; }

        /// <inheritdoc/>
        public string LocalClientSecret { get; }

        /// <inheritdoc/>
        internal string LocalDeviceSecret { get; }

        /// <summary>
        /// Create a new configured realm service 
        /// </summary>
        internal ConfiguredUpstreamRealmSettings(UpstreamConfigurationSection configuration)
        {
            if (configuration.Realm == null)
            {
                throw new InvalidOperationException(string.Format(ErrorMessages.DEPENDENT_PROPERTY_NULL, nameof(UpstreamConfigurationSection.Realm)));
            }
            var realmBuilder = new UriBuilder();
            realmBuilder.Scheme = configuration.Realm.UseTls ? "https" : "http";
            realmBuilder.Host = configuration.Realm.DomainName;
            realmBuilder.Port = configuration.Realm.PortNumber;
            this.Realm = new Uri(realmBuilder.ToString());
            var applicationCredential = configuration.Credentials.Find(o => o.CredentialType == UpstreamCredentialType.Application);
            LocalClientName = applicationCredential?.CredentialName;
            LocalClientSecret = applicationCredential.Conveyance == UpstreamCredentialConveyance.ClientCertificate ? null : applicationCredential.CredentialSecret;
            var deviceCredential = configuration.Credentials.Find(o => o.CredentialType == UpstreamCredentialType.Device);
            LocalDeviceSecret = deviceCredential.Conveyance == UpstreamCredentialConveyance.ClientCertificate ? null : deviceCredential.CredentialSecret;
            LocalDeviceName = deviceCredential.CredentialName;
        }
    }
}
