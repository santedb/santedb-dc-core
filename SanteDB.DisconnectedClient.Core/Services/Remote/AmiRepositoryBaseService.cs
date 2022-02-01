/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Security;
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

        /// <summary>
        /// Get a service client
        /// </summary>
        protected AmiServiceClient GetClient(IPrincipal principal = null)
        {
            var retVal = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

            if (principal == null)
                principal = AuthenticationContext.Current.Principal;

            // Don't allow anonymous principals
            if (!principal.Identity.IsAuthenticated ||
                principal == AuthenticationContext.SystemPrincipal ||
                principal == AuthenticationContext.AnonymousPrincipal)
            {
                using (AuthenticationContextExtensions.TryEnterDeviceContext())
                {
                    retVal.Client.Credentials = retVal.Client.Description.Binding.Security.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);
                }
            }
            else
            {
                retVal.Client.Credentials = retVal.Client.Description.Binding.Security.CredentialProvider.GetCredentials(principal);
            }
            return retVal;
        }


    }
}