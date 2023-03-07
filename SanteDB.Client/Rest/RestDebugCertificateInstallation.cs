using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Certs;
using SanteDB.Rest.Common.Configuration.Interop;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SanteDB.Client.Rest
{
    /// <summary>
    /// Debug certificate installation
    /// </summary>
    public static class RestDebugCertificateInstallation
    {

        /// <summary>
        /// Install the debugger certificate
        /// </summary>
        public static void InstallDebuggerCertificate(Uri bindingBase, ICertificateGeneratorService certificateGeneratorService)
        {
            var sslBindingUtil = HttpSslUtil.GetCurrentPlatformCertificateBinder();
            if (sslBindingUtil == null)
            {
                throw new InvalidOperationException(ErrorMessages.CANNOT_BIND_CERTIFICATES);
            }

            var ssiDebugCert = X509CertificateUtils.FindCertificate(X509FindType.FindBySubjectDistinguishedName, StoreLocation.LocalMachine, StoreName.My, $"CN={bindingBase.Host}");
            if (ssiDebugCert == null)
            {
                var keyPair = certificateGeneratorService.CreateKeyPair(2048);
                ssiDebugCert = certificateGeneratorService.CreateSelfSignedCertificate(keyPair, new X500DistinguishedName($"CN={bindingBase.Host}"), new TimeSpan(365, 0, 0, 0), X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, new string[] { ExtendedKeyUsageOids.ServerAuthentication }, new String[] { bindingBase.Host } );
                X509CertificateUtils.InstallMachineCertificate(ssiDebugCert);
                X509CertificateUtils.InstallCertificate(StoreName.Root, ssiDebugCert);
            }

            try
            {
                if (bindingBase.HostNameType == UriHostNameType.Dns)
                {
                    var ipaddress = Dns.GetHostAddresses(bindingBase.Host);
                    sslBindingUtil.BindCertificate(ipaddress[0], bindingBase.Port, ssiDebugCert.GetCertHash(), false, StoreName.My, StoreLocation.CurrentUser);
                }
                else if (IPAddress.TryParse(bindingBase.Host, out var ipAddress))
                {
                    sslBindingUtil.BindCertificate(ipAddress, bindingBase.Port, ssiDebugCert.GetCertHash(), false, StoreName.My, StoreLocation.CurrentUser);
                }
            }
            catch
            {
                Tracer.GetTracer(typeof(RestDebugCertificateInstallation)).TraceError("Failed to Bind SSL certificate - you may need to run netsh http add sslcert ipport={0}:{1} certhash={2} from an elevated command prompt", bindingBase.Host, bindingBase.Port, ssiDebugCert.Thumbprint);
            }
        }
    }
}
