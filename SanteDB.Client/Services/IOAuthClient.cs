/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-6-21
 */
using SanteDB.Core.Security.Claims;
using System;

namespace SanteDB.Client.Services
{
    /// <summary>
    /// Represents an interface for implementing classes which communicate with an OAuth service
    /// </summary>
    public interface IOAuthClient : IDisposable
    {
        /// <summary>
        /// Authenticate a user given an authenticated device principal and optionally a specific application.
        /// </summary>
        /// <param name="username">The username to pass to the OAUTH service</param>
        /// <param name="password">The password to pass to the OAUTH service</param>
        /// <param name="clientId">Optional client_id to provide in the request. If this value is <c>null</c>, the Realm client_id will be used.</param>
        /// <param name="tfaSecret">The MFA secret collected from the user</param>
        /// <returns>The claims principal if the session was authenticated</returns>
        IClaimsPrincipal AuthenticateUser(string username, string password, string clientId = null, string tfaSecret = null);

        /// <summary>
        /// Authenticate an application given an authenticated device principal.
        /// </summary>
        /// <param name="clientId">The client_id to pass to the oauth service</param>
        /// <param name="clientSecret">The client secret to pass to the oauth service</param>
        /// <param name="onBehalfOfUser">The user under which the authentication request is being made</param>
        /// <returns>The claims principal if the session was authenticated</returns>
        IClaimsPrincipal AuthenticateApp(string clientId, string clientSecret = null);

        /// <summary>
        /// Refresh the current <paramref name="refreshToken"/>
        /// </summary>
        /// <param name="refreshToken">The token which is to be refreshed</param>
        /// <returns>The refreshed session in a <see cref="IClaimsPrincipal"/></returns>
        IClaimsPrincipal Refresh(string refreshToken);

        /// <summary>
        /// Login for the purposes of changing a password only
        /// </summary>
        /// <param name="challengeKey">The key of the challenge which the user is responding to</param>
        /// <param name="clientId">The client_id of the application the user is using</param>
        /// <param name="response">The response to the <paramref name="challengeKey"/></param>
        /// <param name="tfaSecret">The TFA or MFA secret which was collected (if available)</param>
        /// <param name="userName">The name of the user which is being reset</param>
        /// <returns>The <see cref="IClaimsPrincipal"/> which was authenticated with the challenge key</returns>
        IClaimsPrincipal ChallengeAuthenticateUser(string userName, Guid challengeKey, string response, string clientId = null, string tfaSecret = null);
    }
}
