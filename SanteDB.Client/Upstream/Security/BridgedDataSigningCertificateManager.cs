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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// Implementation of <see cref="IDataSigningCertificateManagerService"/> which uses local and fallback to upstream
    /// </summary>
    [PreferredService(typeof(IDataSigningCertificateManagerService))]
    public class BridgedDataSigningCertificateManager : IDataSigningCertificateManagerService
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(BridgedDataSigningCertificateManager));
        private readonly IDataSigningCertificateManagerService m_localServiceProvider;
        private readonly IDataSigningCertificateManagerService m_upstreamServiceProvider;
        private readonly IIdentityProviderService m_identityProvider;
        private readonly IApplicationIdentityProviderService m_applicationIdentityProvider;
        private readonly IDeviceIdentityProviderService m_deviceIdentityProvider;

        /// <summary>
        /// DI ctor
        /// </summary>
        public BridgedDataSigningCertificateManager(
            ILocalServiceProvider<IDataSigningCertificateManagerService> localServiceProvider, 
            IUpstreamServiceProvider<IDataSigningCertificateManagerService> upstreamServiceProvider,
            ILocalServiceProvider<IIdentityProviderService> identityProvider,
            ILocalServiceProvider<IApplicationIdentityProviderService> applicationIdentityProvider,
            ILocalServiceProvider<IDeviceIdentityProviderService> deviceIdentityProvider)
        {

            this.m_localServiceProvider = localServiceProvider.LocalProvider;
            this.m_upstreamServiceProvider = upstreamServiceProvider.UpstreamProvider;
            this.m_identityProvider = identityProvider.LocalProvider;
            this.m_applicationIdentityProvider = applicationIdentityProvider.LocalProvider;
            this.m_deviceIdentityProvider = deviceIdentityProvider.LocalProvider;
        }

        /// <inheritdoc/>
        public string ServiceName => "Bridged Data Signing Certificate Manager Service";

        /// <summary>
        /// Determine if <paramref name="identity"/> is a local identity
        /// </summary>
        private bool IsLocalIdentity(IIdentity identity)
        {
            switch(identity)
            {
                case IApplicationIdentity iai:
                    return this.m_applicationIdentityProvider.GetClaims(iai.Name).Any(o => o.Type == SanteDBClaimTypes.LocalOnly);
                case IDeviceIdentity idi:
                    return this.m_deviceIdentityProvider.GetClaims(idi.Name).Any(o => o.Type == SanteDBClaimTypes.LocalOnly);
                default:
                    return this.m_identityProvider.GetClaims(identity.Name).Any(o => o.Type == SanteDBClaimTypes.LocalOnly);
            }
        }

        /// <inheritdoc/>
        public void AddSigningCertificate(IIdentity identity, X509Certificate2 x509Certificate, IPrincipal principal)
        {
            if(this.IsLocalIdentity(identity))
            {
                this.m_localServiceProvider.AddSigningCertificate(identity, x509Certificate, principal);
            }
            else
            {
                this.m_upstreamServiceProvider.AddSigningCertificate(identity, x509Certificate, principal);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<X509Certificate2> GetSigningCertificates(IIdentity identity)
        {
            if (this.IsLocalIdentity(identity))
            {
                return this.m_localServiceProvider.GetSigningCertificates(identity);
            }
            else
            {
                return this.m_upstreamServiceProvider.GetSigningCertificates(identity);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<X509Certificate2> GetSigningCertificates(Type classOfIdentity, NameValueCollection filter)
        {
            var results = this.m_localServiceProvider.GetSigningCertificates(classOfIdentity, filter);
            if(!results.Any())
            {
                results = this.m_upstreamServiceProvider.GetSigningCertificates(classOfIdentity, filter);
            }
            return results;
        }

        /// <inheritdoc/>
        public void RemoveSigningCertificate(IIdentity identity, X509Certificate2 x509Certificate, IPrincipal principal)
        {
            if (this.IsLocalIdentity(identity))
            {
                this.m_localServiceProvider.RemoveSigningCertificate(identity, x509Certificate, principal);
            }
            else
            {
                this.m_upstreamServiceProvider.RemoveSigningCertificate(identity, x509Certificate, principal);
            }
        }

        /// <inheritdoc/>
        public bool TryGetSigningCertificateByHash(byte[] x509hash, out X509Certificate2 certificate)
            => this.TryGetSigningCertificateByThumbprint(x509hash.HexEncode(), out certificate);

        /// <inheritdoc/>
        public bool TryGetSigningCertificateByThumbprint(string x509Thumbprint, out X509Certificate2 certificate)
        {

            return this.m_localServiceProvider.TryGetSigningCertificateByThumbprint(x509Thumbprint, out certificate) ||
                this.m_upstreamServiceProvider.TryGetSigningCertificateByThumbprint(x509Thumbprint, out certificate);
        }

        /// <inheritdoc/>
        public IEnumerable<IIdentity> GetCertificateIdentities(X509Certificate2 certificate)
        {
            return this.m_localServiceProvider.GetCertificateIdentities(certificate);
        }
    }
}
