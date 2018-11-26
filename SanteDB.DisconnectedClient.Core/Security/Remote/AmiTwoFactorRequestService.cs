/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-6-28
 */
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.DisconnectedClient.Core.Security.Remote
{
    /// <summary>
    /// AMI based password reset service
    /// </summary>
    public class AmiTwoFactorRequestService : ITwoFactorRequestService
    {

        // Authentication context
        private AuthenticationContext m_authContext;

        /// <summary>
        /// Ensure the current client is authenticated
        /// </summary>
        private void EnsureAuthenticated()
        {
            var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            if (this.m_authContext == null ||
                ((this.m_authContext.Principal as ClaimsPrincipal)?.FindClaim(ClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MinValue) < DateTime.Now)
                this.m_authContext = new AuthenticationContext(ApplicationContext.Current.GetService<IIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret));
        }

        /// <summary>
        /// Get the reset mechanisms
        /// </summary>
        public List<TfaMechanismInfo> GetResetMechanisms()
        {
            this.EnsureAuthenticated();
            using (AmiServiceClient amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami")))
            {
                var authContext = AuthenticationContext.Current;
                AuthenticationContext.Current = this.m_authContext;
                var retVal = amiClient.GetTwoFactorMechanisms().CollectionItem.OfType<TfaMechanismInfo>().ToList();
                AuthenticationContext.Current = authContext;
                return retVal;
            }
        }


        /// <summary>
        /// Send the verification code
        /// </summary>
        public void SendVerificationCode(Guid mechanism, string challengeResponse, string userName, string scope)
        {
            this.EnsureAuthenticated();
            using (AmiServiceClient amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami")))
            {
                var authContext = AuthenticationContext.Current;
                AuthenticationContext.Current = this.m_authContext;

                // Next I have to request a TFA secret!!
                amiClient.SendTfaSecret(new TfaRequestInfo()
                {
                    ResetMechanism = mechanism,
                    Verification = challengeResponse,
                    UserName = userName,
                    Purpose = scope
                });

                AuthenticationContext.Current = authContext;
            }
        }
    }
}
