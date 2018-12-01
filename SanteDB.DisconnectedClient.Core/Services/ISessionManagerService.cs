﻿/*
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
using SanteDB.DisconnectedClient.Core.Security;
using System;
using System.Security.Principal;
using SanteDB.Core.Services;

namespace SanteDB.DisconnectedClient.Core.Services
{
    /// <summary>
    /// Represents a session manager service
    /// </summary>
    public interface ISessionManagerService
    {

        /// <summary>
        /// Authenticates the specified username/password pair
        /// </summary>
        SessionInfo Authenticate(String userName, String password);

        /// <summary>
        /// Authenticates the specified username/password/tfasecret pair
        /// </summary>
        SessionInfo Authenticate(String userName, String password, String tfaSecret);

        /// <summary>
        /// Authenticate with PIN (offline local context only)
        /// </summary>
        SessionInfo Authenticate(String userName, byte[] pin, params Claim[] claims);
        
        /// <summary>
        /// Refreshes the specified session
        /// </summary>
        /// <param name="principal"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        SessionInfo Refresh(SessionInfo session);

        /// <summary>
        /// Deletes (abandons) the session
        /// </summary>
        SessionInfo Delete(IPrincipal sessionPrincipal);

        /// <summary>
        /// Gets the session
        /// </summary>
        SessionInfo Get(IPrincipal sessionId);

        /// <summary>
        /// Gets the session from the specified session token
        /// </summary>
        SessionInfo Get(String sessionToken);
    }
}
