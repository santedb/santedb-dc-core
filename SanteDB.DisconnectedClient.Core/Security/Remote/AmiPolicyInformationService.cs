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
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Core.Security
{
    /// <summary>
    /// Policy information service which feeds from AMI
    /// </summary>
    public class AmiPolicyInformationService : IPolicyInformationService
    {
        // Service client
        private AmiServiceClient m_client = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

        /// <summary>
        /// Remote policies only
        /// </summary>
        public AmiPolicyInformationService()
        {

        }

        /// <summary>
        /// Policy as a specific principal
        /// </summary>
        public AmiPolicyInformationService(ClaimsPrincipal cprincipal)
        {
            this.m_client.Client.Credentials = this.m_client.Client?.Description?.Binding?.Security?.CredentialProvider?.GetCredentials(cprincipal);
        }

        /// <summary>
        /// Get active policies for the specified securable
        /// </summary>
        public IEnumerable<IPolicyInstance> GetActivePolicies(object securable)
        {
            // Security device
            if (securable is SecurityDevice)
                throw new NotImplementedException();
            else if (securable is SecurityRole)
            {
                string name = (securable as SecurityRole).Name;
                return this.m_client.FindRole(o => o.Name == name).CollectionItem.OfType<SecurityRoleInfo>().First().Policies.Select(o => new GenericPolicyInstance(new GenericPolicy(o.Oid, o.Name, o.CanOverride), o.Grant)).ToList();

            }
            else if (securable is SecurityApplication)
                throw new NotImplementedException();
            else if (securable is IPrincipal || securable is IIdentity)
            {
                var userInfo = this.m_client.GetUsers(o => o.UserName == (securable as IPrincipal).Identity.Name).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault();
                if (userInfo != null)
                    return this.GetActivePolicies(new SecurityRole() { Name = userInfo.Roles.FirstOrDefault() });
                else
                    return new List<IPolicyInstance>();

            }
            else if (securable is Act)
                throw new NotImplementedException();
            else if (securable is Entity)
                throw new NotImplementedException();
            else
                return new List<IPolicyInstance>();
        }

        public IEnumerable<IPolicy> GetPolicies()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the specified policy from the AMI
        /// </summary>
        public IPolicy GetPolicy(string policyOid)
        {
            return this.m_client.FindPolicy(p => p.Oid == policyOid).CollectionItem.OfType<SecurityPolicy>().Select(o => new GenericPolicy(o.Oid, o.Name, o.CanOverride)).FirstOrDefault();
        }
    }
}
