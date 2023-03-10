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
