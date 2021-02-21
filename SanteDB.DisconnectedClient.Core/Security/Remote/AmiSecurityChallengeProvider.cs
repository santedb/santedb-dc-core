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
using SanteDB.Core;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Services.Remote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Security.Remote
{
    /// <summary>
    /// Security challenge provider which fetches a user's challenge responses from a server
    /// </summary>
    public class AmiSecurityChallengeProvider : AmiRepositoryBaseService, ISecurityChallengeService
    {
        /// <summary>
        /// Gets the service name for the security challenge service
        /// </summary>
        public string ServiceName => "Remote Security Challenge Service";

        /// <summary>
        /// Get the security challenges from the server (actually typically 
        /// </summary>
        public IEnumerable<SecurityChallenge> Get(string userName, IPrincipal principal)
        {
            // Is this user a local user?
            if (ApplicationServiceContext.Current.GetService<IOfflineIdentityProviderService>()?.IsLocalUser(userName) == true)
                return ApplicationServiceContext.Current.GetService<IOfflineSecurityChallengeService>()?.Get(userName, principal);
            else using (var client = this.GetClient())
                {
                    // Get the user name
                    var user = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>().GetUser(userName);
                    if (user == null)
                        user = client.GetUsers(o => o.UserName == userName).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault()?.Entity;
                    if (user == null) // local user?
                        throw new KeyNotFoundException($"User {userName} does not exist");
                    return client.Client.Get<AmiCollection>($"SecurityUser/{user.Key}/challenge").CollectionItem.OfType<SecurityChallenge>();
                }
        }

        /// <summary>
        /// Get the security challenges from the server (actually typically 
        /// </summary>
        public IEnumerable<SecurityChallenge> Get(Guid userKey, IPrincipal principal)
        {
            // Is this user a local user?
            if (ApplicationServiceContext.Current.GetService<IOfflineIdentityProviderService>()?.IsLocalUser(userKey) == true)
                return ApplicationServiceContext.Current.GetService<IOfflineSecurityChallengeService>()?.Get(userKey, principal);
            else using (var client = this.GetClient())
                {
                    // Get the user name
                    return client.Client.Get<AmiCollection>($"SecurityUser/{userKey}/challenge").CollectionItem.OfType<SecurityChallenge>();
                }
        }


        /// <summary>
        /// Remove a security challenge for the user
        /// </summary>
        public void Remove(string userName, Guid challengeKey, IPrincipal principal)
        {
            // Is this user a local user?
            if (ApplicationServiceContext.Current.GetService<IOfflineIdentityProviderService>()?.IsLocalUser(userName) == true)
                ApplicationServiceContext.Current.GetService<IOfflineSecurityChallengeService>()?.Remove(userName, challengeKey, principal);
            else using (var client = this.GetClient(principal))
                {
                    // CLIENT MUST BE USING THEIR OWN CREDENTIAL
                    // NOTE: Contains here is used to gneerate the ~ operator, it does not perform the operation as a traditional .NET contains
                    var user = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>().GetUser(userName);
                    if (user == null)
                        user = client.GetUsers(o => o.UserName == userName).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault()?.Entity;
                    if (user == null)
                        throw new KeyNotFoundException($"User {userName} does not exist");

                    client.Client.Delete<SecurityChallenge>($"SecurityUser/{user.Key}/challenge/{challengeKey}");
                }

        }

        /// <summary>
        /// Set the answer to a challenge response
        /// </summary>
        public void Set(string userName, Guid challengeKey, string response, IPrincipal principal)
        {
            // Is this user a local user?
            if (ApplicationServiceContext.Current.GetService<IOfflineIdentityProviderService>()?.IsLocalUser(userName) == true)
                ApplicationServiceContext.Current.GetService<IOfflineSecurityChallengeService>()?.Set(userName, challengeKey, response, principal);
            else using (var client = this.GetClient(principal))
                {
                    // CLIENT MUST BE USING THEIR OWN CREDENTIAL
                    var user = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>().GetUser(userName);
                    if (user == null)
                        user = client.GetUsers(o => o.UserName == userName).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault()?.Entity;
                    if (user == null)
                        throw new KeyNotFoundException($"User {userName} does not exist");
                    var challengeSet = new SecurityUserChallengeInfo()
                    {
                        ChallengeKey = challengeKey,
                        ChallengeResponse = response
                    };
                    client.Client.Post<SecurityUserChallengeInfo, Object>($"SecurityUser/{user.Key}/challenge", client.Client.Accept, challengeSet);
                }
        }
    }
}
