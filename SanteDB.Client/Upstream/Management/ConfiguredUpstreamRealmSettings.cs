using SanteDB.Client.Configuration.Upstream;
using SanteDB.Core.i18n;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

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

            var applicationCredential = configuration.Credentials.Find(o => o.CredentialType == UpstreamCredentialType.Application);
            LocalClientName = applicationCredential?.CredentialName;
            LocalClientSecret = applicationCredential.Conveyance == UpstreamCredentialConveyance.ClientCertificate ? null : applicationCredential.CredentialSecret;
            var deviceCredential = configuration.Credentials.Find(o => o.CredentialType == UpstreamCredentialType.Device);
            LocalDeviceSecret = deviceCredential.Conveyance == UpstreamCredentialConveyance.ClientCertificate ? null : deviceCredential.CredentialSecret;
            LocalDeviceName = deviceCredential.CredentialName;
        }
    }
}
