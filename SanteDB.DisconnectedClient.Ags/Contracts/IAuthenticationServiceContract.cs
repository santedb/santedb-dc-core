/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
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
using RestSrvr.Attributes;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Xamarin.Security;
using System;
using System.Collections.Specialized;

namespace SanteDB.DisconnectedClient.Ags.Contracts
{
    /// <summary>
    /// Authentication service contract
    /// </summary>
    [ServiceContract(Name = "AUTH")]
    public interface IAuthenticationServiceContract
    {

        /// <summary>
        /// Authenticate the request
        /// </summary>
        [Post("oauth2_token")]
        OAuthTokenResponse AuthenticateOAuth(NameValueCollection request);

        /// <summary>
        /// Authenticate the request
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Post("session")]
        SessionInfo Authenticate(NameValueCollection request);

        /// <summary>
        /// Get the session
        /// </summary>
        [Get("session")]
        SessionInfo GetSession();

        /// <summary>
        /// Delete (abandon) the session
        /// </summary>
        [Delete("session")]
        void AbandonSession();

        /// <summary>
        /// Gets an policy decision for the specified policy
        /// </summary>
        [Get("pdp/{policyId}")]
        void AclPreCheck(String policyId);
    }
}
