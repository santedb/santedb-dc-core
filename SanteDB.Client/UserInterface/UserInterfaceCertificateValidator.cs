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
