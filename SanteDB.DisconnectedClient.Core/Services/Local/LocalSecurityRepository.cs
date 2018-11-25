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
 * Date: 2018-6-22
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Represents a security repository service that uses the direct local services
    /// </summary>
    public class LocalSecurityRepository : ISecurityRepositoryService,
        ISecurityAuditEventSource,
        ISecurityInformationService
    {
        private Tracer m_traceSource = Tracer.GetTracer(typeof(LocalSecurityRepository));

        /// <summary>
        /// Indicates security attributes have changed
        /// </summary>
        public event EventHandler<SecurityAuditDataEventArgs> SecurityAttributesChanged;
        public event EventHandler<SecurityAuditDataEventArgs> SecurityResourceCreated;
        public event EventHandler<SecurityAuditDataEventArgs> SecurityResourceDeleted;

        /// <summary>
        /// Demand permission
        /// </summary>
        protected void Demand(String policyId)
        {
            var pdp = ApplicationContext.Current.GetService<IPolicyDecisionService>();
            var outcome = pdp?.GetPolicyOutcome(AuthenticationContext.Current.Principal, policyId);
            if (outcome != PolicyGrantType.Grant)
                throw new PolicyViolationException(policyId, outcome ?? PolicyGrantType.Deny);

        }

        /// <summary>
        /// Add users to roles
        /// </summary>
        public void AddUsersToRoles(string[] users, string[] roles)
        {
            this.Demand(PermissionPolicyIdentifiers.CreateRoles);
            ApplicationContext.Current.GetService<IRoleProviderService>().AddUsersToRoles(users, roles, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Changes a user's password.
        /// </summary>
        /// <param name="userId">The id of the user.</param>
        /// <param name="password">The new password of the user.</param>
        /// <returns>Returns the updated user.</returns>
        public SecurityUser ChangePassword(Guid userId, string password)
        {

            this.m_traceSource.TraceWarning("Changing user password");
            var securityUser = ApplicationContext.Current.GetService<IRepositoryService<SecurityUser>>()?.Get(userId);
            if (securityUser == null)
                throw new KeyNotFoundException("Cannot locate security user");

            if (!securityUser.UserName.Equals(AuthenticationContext.Current.Principal.Identity.Name, StringComparison.OrdinalIgnoreCase))
                this.Demand(PermissionPolicyIdentifiers.ChangePassword);

            var iids = ApplicationContext.Current.GetService<IIdentityProviderService>();
            if (iids == null) throw new InvalidOperationException("Cannot find identity provider service");
            iids.ChangePassword(securityUser.UserName, password, AuthenticationContext.Current.Principal);
            this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(securityUser, "Password"));
            return securityUser;
        }

        /// <summary>
        /// Change password
        /// </summary>
        public void ChangePassword(string userName, string password)
        {
            if (!userName.Equals(AuthenticationContext.Current.Principal.Identity.Name, StringComparison.OrdinalIgnoreCase))
                this.Demand(PermissionPolicyIdentifiers.ChangePassword);
            ApplicationContext.Current.GetService<IIdentityProviderService>().ChangePassword(userName, password, AuthenticationContext.Current.Principal);
            this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(userName, "Password"));
        }

        /// <summary>
        /// Creates a user with a specified password.
        /// </summary>
        /// <param name="userInfo">The security user.</param>
        /// <param name="password">The password.</param>
        /// <returns>Returns the newly created user.</returns>
        public SecurityUser CreateUser(SecurityUser userInfo, string password)
        {
            this.Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction);
            userInfo.Password = password;
            return ApplicationContext.Current.GetService<IRepositoryService<SecurityUser>>().Insert(userInfo);
        }

        /// <summary>
        /// Get all active policies
        /// </summary>
        public IEnumerable<SecurityPolicyInstance> GetActivePolicies(object securable)
        {
            return ApplicationContext.Current.GetService<IPolicyInformationService>().GetActivePolicies(securable).Select(o => o.ToPolicyInstance());
        }

        /// <summary>
        /// Get all roles from db
        /// </summary>
        public string[] GetAllRoles()
        {
            return ApplicationContext.Current.GetService<IRoleProviderService>().GetAllRoles();
        }


        /// <summary>
        /// Get the policy information in the model format
        /// </summary>
        public SecurityPolicy GetPolicy(string policyOid)
        {
            int tr = 0;
            return ApplicationContext.Current.GetService<IRepositoryService<SecurityPolicy>>().Find(o => o.Oid == policyOid, 0, 1, out tr).SingleOrDefault();
        }

        /// <summary>
        /// Get the security provenance 
        /// </summary>
        public SecurityProvenance GetProvenance(Guid provenanceId)
        {
            return ApplicationContext.Current.GetService<IDataPersistenceService<SecurityProvenance>>().Get(provenanceId);
        }

        /// <summary>
        /// Get the specified role 
        /// </summary>
        public SecurityRole GetRole(string roleName)
        {
            int tr = 0;
            return ApplicationContext.Current.GetService<IRepositoryService<SecurityRole>>()?.Find(o => o.Name == roleName, 0, 1, out tr).SingleOrDefault();
        }

        /// <summary>
        /// Gets a specific user.
        /// </summary>
        /// <param name="userName">The id of the user to retrieve.</param>
        /// <returns>Returns the user.</returns>
        public SecurityUser GetUser(String userName)
        {
            int tr = 0;
            // As the identity service may be LDAP, best to call it to get an identity name
            var identity = ApplicationContext.Current.GetService<IIdentityProviderService>().GetIdentity(userName);
            return ApplicationContext.Current.GetService<IRepositoryService<SecurityUser>>().Find(u => u.UserName == identity.Name, 0, 1, out tr).FirstOrDefault();
        }

        /// <summary>
        /// Get the specified user based on identity
        /// </summary>
		public SecurityUser GetUser(IIdentity identity)
        {
            return this.GetUser(identity.Name);
        }

        /// <summary>
        /// Get user entity from identity
        /// </summary>
        public UserEntity GetUserEntity(IIdentity identity)
        {
            int t = 0;
            return ApplicationContext.Current.GetService<IRepositoryService<UserEntity>>()?.Find(o => o.SecurityUser.UserName == identity.Name, 0, 1, out t).FirstOrDefault();
        }

        /// <summary>
        /// Determine if user is in role
        /// </summary>
        public bool IsUserInRole(string user, string role)
        {
            return ApplicationContext.Current.GetService<IRoleProviderService>().IsUserInRole(user, role);
        }

        /// <summary>
        /// Locks a specific user.
        /// </summary>
        /// <param name="userId">The id of the user to lock.</param>
		public void LockUser(Guid userId)
        {
            this.Demand(PermissionPolicyIdentifiers.AlterIdentity);

            this.m_traceSource.TraceWarning("Locking user {0}", userId);

            var iids = ApplicationContext.Current.GetService<IIdentityProviderService>();
            if (iids == null)
                throw new InvalidOperationException("Missing identity provider service");

            var securityUser = ApplicationContext.Current.GetService<IRepositoryService<SecurityUser>>()?.Get(userId);
            if (securityUser == null)
                throw new KeyNotFoundException(userId.ToString());
            iids.SetLockout(securityUser.UserName, true);
            this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(securityUser, "Lockout=True"));
        }

        /// <summary>
        /// Remove user from roles
        /// </summary>
        public void RemoveUsersFromRoles(string[] users, string[] roles)
        {
            throw new NotSupportedException();
        }


        /// <summary>
        /// Unlocks a specific user.
        /// </summary>
        /// <param name="userId">The id of the user to be unlocked.</param>
        public void UnlockUser(Guid userId)
        {
            this.Demand(PermissionPolicyIdentifiers.AlterIdentity);
            this.m_traceSource.TraceWarning("Unlocking user {0}", userId);

            var iids = ApplicationContext.Current.GetService<IIdentityProviderService>();
            if (iids == null)
                throw new InvalidOperationException("Missing identity provider service");

            var securityUser = ApplicationContext.Current.GetService<IRepositoryService<SecurityUser>>()?.Get(userId);
            if (securityUser == null)
                throw new KeyNotFoundException(userId.ToString());
            iids.SetLockout(securityUser.UserName, false);
            this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(securityUser, "Lockout=False"));

        }

    }
}