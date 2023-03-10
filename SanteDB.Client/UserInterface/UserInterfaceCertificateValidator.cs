/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SanteDB.Client.UserInterface
{
    /// <summary>
    /// Certificate validator for remote certificates
    /// </summary>
    public class UserInterfaceCertificateValidator : ICertificateValidator
    {
        private readonly ILocalizationService m_localizationService;
        private readonly IUserInterfaceInteractionProvider m_interactionProvider;

        /// <summary>
        /// DI constructor
        /// </summary>
        public UserInterfaceCertificateValidator(IUserInterfaceInteractionProvider interactionProvider, ILocalizationService localizationService)
        {
            this.m_localizationService = localizationService;
            this.m_interactionProvider = interactionProvider;
        }

        /// <inheritdoc/>
        public bool ValidateCertificate(X509Certificate2 certificate, X509Chain chain)
        {
            if (certificate == null || chain == null)
                return false;
            else
            {
                var trustedCertificate = X509CertificateUtils.FindCertificate(X509FindType.FindBySubjectName, StoreLocation.CurrentUser, StoreName.TrustedPeople, certificate.Subject);
                if (trustedCertificate == null)
                {
                    if (this.m_interactionProvider.Confirm(this.m_localizationService.GetString(UserMessageStrings.CONFIRM_CERTIFICATE_TRUST, new { cert = certificate.Subject })))
                    {
                        X509CertificateUtils.InstallCertificate(StoreName.TrustedPeople, new X509Certificate2(certificate));
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
