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
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Services
{
    public interface IOAuthClient : IDisposable
    {
        /// <summary>
        /// Authenticate a user given an authenticated device principal and optionally a specific application.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="clientId">Optional client_id to provide in the request. If this value is <c>null</c>, the Realm client_id will be used.</param>
        /// <param name="devicePrincipal"></param>
        /// <param name="tfaSecret"></param>
        /// <returns></returns>
        IClaimsPrincipal AuthenticateUser(string username, string password, string clientId = null, string tfaSecret = null);

        /// <summary>
        /// Authenticate an application given an authenticated device principal.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        /// <returns></returns>
        IClaimsPrincipal AuthenticateApp(string clientId, string clientSecret = null);

        IClaimsPrincipal Refresh(string refreshToken);

        /// <summary>
        /// Login for the purposes of changing a password only
        /// </summary>
        IClaimsPrincipal ChallengeAuthenticateUser(string userName, Guid challengeKey, string response, string clientId = null, string tfaSecret = null);
    }
}
