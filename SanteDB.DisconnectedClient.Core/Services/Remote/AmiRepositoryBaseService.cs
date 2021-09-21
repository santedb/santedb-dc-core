/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Represetns a base service that uses the AMI
    /// </summary>
    public abstract class AmiRepositoryBaseService
    {

        // The principal to fall back on
        private IPrincipal m_devicePrincipal;

        /// <summary>
        /// Get a service client
        /// </summary>
        protected AmiServiceClient GetClient(IPrincipal principal = null)
        {
            var retVal = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

            var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();
            if (principal == null)
                principal = AuthenticationContext.Current.Principal;

            // Don't allow anonymous principals
            if (!principal.Identity.IsAuthenticated ||
                principal == AuthenticationContext.SystemPrincipal ||
                principal == AuthenticationContext.AnonymousPrincipal)
            {
                principal = this.m_devicePrincipal;
                // Expired or not exists
                if (principal == null || ((principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MinValue) < DateTime.Now)
                    this.m_devicePrincipal = principal = ApplicationContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret);
            }

            retVal.Client.Credentials = retVal.Client.Description.Binding.Security.CredentialProvider.GetCredentials(principal);
            return retVal;
        }


    }
}