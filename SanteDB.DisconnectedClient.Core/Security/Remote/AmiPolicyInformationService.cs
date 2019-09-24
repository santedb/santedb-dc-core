/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Core.Services.Remote;
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
    public class AmiPolicyInformationService : AmiRepositoryBaseService, IPolicyInformationService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "AMI Remote Policy Information Service";

        /// <summary>
        /// Remote policies only
        /// </summary>
        public AmiPolicyInformationService()
        {

        }

        /// <summary>
        /// Creates a new AMI PIP
        /// </summary>
        public AmiPolicyInformationService(IPrincipal principal)
        {
            this.m_cachedCredential = principal;
        }

        /// <summary>
        /// Note supported
        /// </summary>
        public void AddPolicies(object securable, PolicyGrantType rule, IPrincipal principal, params string[] policyOids)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            foreach (var itm in policyOids) {
                this.m_client.Client.Post<SecurityPolicyInfo, SecurityPolicyInfo>($"{securable.GetType().Name}/{(securable as IIdentifiedEntity).Key}/policy", this.m_client.Client.Accept, new SecurityPolicyInfo()
                {
                    Oid = itm, 
                    Grant = rule
                });
            }
        }

        /// <summary>
        /// Note supported
        /// </summary>
        public void RemovePolicies(object securable, IPrincipal principal, params string[] policyOids)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            foreach (var itm in policyOids)
            {
                var pol = this.GetPolicy(itm);
                this.m_client.Client.Delete<object>($"{securable.GetType().Name}/{(securable as IIdentifiedEntity).Key}/policy/{pol.Key}");
            }
        }

        /// <summary>
        /// Get active policies for the specified securable
        /// </summary>
        public IEnumerable<IPolicyInstance> GetActivePolicies(object securable)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            // Security device
            if (securable is SecurityDevice)
            {
                string name = (securable as SecurityDevice).Name;
                return this.m_client.GetDevices(o => o.Name == name).CollectionItem.OfType<SecurityDeviceInfo>().First().Policies.Select(o => new GenericPolicyInstance(new GenericPolicy(o.Policy.Key.Value, o.Oid, o.Name, o.CanOverride), o.Grant)).ToList();
            }
            else if (securable is SecurityRole)
            {
                string name = (securable as SecurityRole).Name;
                return this.m_client.FindRole(o => o.Name == name).CollectionItem.OfType<SecurityRoleInfo>().First().Policies.Select(o => new GenericPolicyInstance(new GenericPolicy(o.Policy.Key.Value, o.Oid, o.Name, o.CanOverride), o.Grant)).ToList();

            }
            else if (securable is SecurityApplication)
            {
                string name = (securable as SecurityApplication).Name;
                return this.m_client.GetApplications(o => o.Name == name).CollectionItem.OfType<SecurityApplicationInfo>().First().Policies.Select(o => new GenericPolicyInstance(new GenericPolicy(o.Policy.Key.Value, o.Oid, o.Name, o.CanOverride), o.Grant)).ToList();
            }
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

        /// <summary>
        /// Get all policies
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPolicy> GetPolicies()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the specified policy from the AMI
        /// </summary>
        public IPolicy GetPolicy(string policyOid)
        {
            this.m_client.Client.Credentials = this.GetCredentials();
            return this.m_client.FindPolicy(p => p.Oid == policyOid).CollectionItem.OfType<SecurityPolicy>().Select(o => new GenericPolicy(o.Key.Value, o.Oid, o.Name, o.CanOverride)).FirstOrDefault();
        }


        /// <summary>
        /// Gets the specified policy instance (if applicable) for the specified object
        /// </summary>
        public IPolicyInstance GetPolicyInstance(object securable, string policyOid)
        {
            // TODO: Add caching for this
            return this.GetActivePolicies(securable).FirstOrDefault(o => o.Policy.Oid == policyOid);
        }
    }
}
