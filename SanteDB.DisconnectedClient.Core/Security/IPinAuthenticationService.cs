/*
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient.Services;
using System;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents an authentication service which can authenticate a user using a PIN which is a local secret for this device only
    /// </summary>
    public interface IPinAuthenticationService : IIdentityProviderService
    {

        /// <summary>
        /// Authenticate with a numeric PIN
        /// </summary>
        /// <param name="username">The user being authenticated</param>
        /// <param name="pin">The PIN number digits</param>
        /// <returns>The authenticated principal</returns>
        IPrincipal Authenticate(String username, byte[] pin);

        /// <summary>
        /// Change the user's PIN number
        /// </summary>
        /// <param name="userName">The name of the user to change PIN for</param>
        /// <param name="pin">The PIN to change to</param>
        void ChangePin(String userName, byte[] pin, IPrincipal principal);
    }

}
