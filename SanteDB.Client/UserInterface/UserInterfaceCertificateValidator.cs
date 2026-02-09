/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System.Security.Cryptography.X509Certificates;

namespace SanteDB.Client.UserInterface
{
    /// <summary>
    /// Certificate validator for remote certificates
    /// </summary>
    public class UserInterfaceCertificateValidator : ICertificateValidator
    {
        private readonly ILocalizationService m_localizationService;
        private readonly IUserInterfaceInteractionProvider m_interactionProvider;
        private readonly IPlatformSecurityProvider m_platformSecurityProvider;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(UserInterfaceCertificateValidator));

        /// <summary>
        /// DI constructor
        /// </summary>
        public UserInterfaceCertificateValidator(IUserInterfaceInteractionProvider interactionProvider, IPlatformSecurityProvider platformSecurityProvider, ILocalizationService localizationService)
        {
            this.m_localizationService = localizationService;
            this.m_interactionProvider = interactionProvider;
            this.m_platformSecurityProvider = platformSecurityProvider;
        }

        /// <inheritdoc/>
        public bool ValidateCertificate(X509Certificate2 certificate, X509Chain chain)
        {
            if (certificate == null || chain == null)
            {
                this.m_tracer.TraceWarning("Validation of certificate callback without a certificate or chain validation error");
                return false;
            }
            else
            {
                if (!this.m_platformSecurityProvider.TryGetCertificate(X509FindType.FindByThumbprint, certificate.Thumbprint, storeName: StoreName.TrustedPeople, out var trustedCertificate) &&
                    !this.m_platformSecurityProvider.TryGetCertificate(X509FindType.FindByThumbprint, certificate.Thumbprint, out trustedCertificate))
                {
                    if (this.m_interactionProvider.Confirm(this.m_localizationService.GetString(UserMessageStrings.CONFIRM_CERTIFICATE_TRUST, new { cert = certificate.Subject })))
                    {
                        this.m_tracer.TraceInfo("Installing {0} to CurrentUser/TrustedPeople via {1}", certificate.Subject, this.m_platformSecurityProvider.GetType().Name);
                        if(!this.m_platformSecurityProvider.TryInstallCertificate(new X509Certificate2(certificate), StoreName.TrustedPeople) &&
                            !this.m_platformSecurityProvider.TryInstallCertificate(new X509Certificate2(certificate))
                        )
                        {
                            this.m_tracer.TraceWarning("Could not install certificate to CurrentUser/TrustedPeople");
                        }
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
                //isValid &= chain.ChainStatus.Length == 0;
            }
        }
    }
}
