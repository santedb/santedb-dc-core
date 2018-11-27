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
 * Date: 2018-11-23
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.Messaging.HDSI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Core.Services.Remote
{
    /// <summary>
    /// Represents a remote security repository
    /// </summary>
    public class RemoteSecurityRepository : AmiRepositoryBaseService,
        ISecurityRepositoryService,
        IRepositoryService<SecurityUser>,
        IRepositoryService<SecurityApplication>,
        IRepositoryService<SecurityDevice>,
        IRepositoryService<SecurityRole>,
        IRepositoryService<SecurityPolicy>,
        IRepositoryService<UserEntity>,
        IRepositoryService<SecurityProvenance>
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Remote Security Repository Service";

        // Get a tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteSecurityRepository));

        /// <summary>
        /// Add users to the specified roles
        /// </summary>
        public void AddUsersToRoles(string[] users, string[] roles)
        {
            this.GetCredentials();
            try
            {
                foreach (var usr in users)
                {
                    var user = this.GetUser(usr);
                    if (user == null)
                        throw new KeyNotFoundException($"{usr} not found");

                    this.m_tracer.TraceInfo("Assigning user {0} to roles {1}", usr, String.Join(",", roles));
                    this.m_client.UpdateUser(user.Key.Value, new SanteDB.Core.Model.AMI.Auth.SecurityUserInfo()
                    {
                        Entity = user,
                        Roles = new List<string>(roles)
                    });
                }
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not add users to roles", e);
            }
        }

        /// <summary>
        /// Change user's password
        /// </summary>
        public void ChangePassword(string userName, string password)
        {
            this.GetCredentials();

            try
            {
                var user = this.GetUser(userName);
                if (user == null)
                    throw new KeyNotFoundException($"{userName} not found");
                user.Password = password;
                this.m_client.UpdateUser(user.Key.Value, new SanteDB.Core.Model.AMI.Auth.SecurityUserInfo(user)
                {
                    PasswordOnly = true
                });
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not change password", e);
            }
        }

        /// <summary>
        /// Change password for user
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public SecurityUser ChangePassword(Guid userId, string password)
        {
            this.GetCredentials();

            try
            {
                var user = this.GetUser(userId);
                if (user == null)
                    throw new KeyNotFoundException($"{userId} not found");
                user.Password = password;
                user = this.m_client.UpdateUser(user.Key.Value, new SanteDB.Core.Model.AMI.Auth.SecurityUserInfo(user)
                {
                    PasswordOnly = true
                }).Entity;
                return user;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not change password", e);
            }
        }

        /// <summary>
        /// Create an application
        /// </summary>
        public SecurityApplication CreateApplication(SecurityApplication application)
        {
            this.GetCredentials();

            try
            {
                var retVal = this.m_client.CreateApplication(new SanteDB.Core.Model.AMI.Auth.SecurityApplicationInfo(application)
                {
                    Policies = application.Policies.Select(o => new SanteDB.Core.Model.AMI.Auth.SecurityPolicyInfo(o)).ToList()
                });
                retVal.Entity.Policies = retVal.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return retVal.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not create application", e);
            }
        }

        /// <summary>
        /// Create security device
        /// </summary>
        public SecurityDevice CreateDevice(SecurityDevice device)
        {
            this.GetCredentials();

            try
            {
                var retVal = this.m_client.CreateDevice(new SanteDB.Core.Model.AMI.Auth.SecurityDeviceInfo(device)
                {
                    Policies = device.Policies.Select(o => new SanteDB.Core.Model.AMI.Auth.SecurityPolicyInfo(o)).ToList()
                });
                retVal.Entity.Policies = retVal.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return retVal.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not create device", e);
            }
        }

        /// <summary>
        /// Create a security policy
        /// </summary>
        public SecurityPolicy CreatePolicy(SecurityPolicy policy)
        {
            this.GetCredentials();

            try
            {
                return this.m_client.CreatePolicy(new SanteDB.Core.Model.AMI.Auth.SecurityPolicyInfo(policy)).Policy;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not create policy", e);
            }
        }

        /// <summary>
        /// Create a role
        /// </summary>
        public SecurityRole CreateRole(SecurityRole roleInfo)
        {
            this.GetCredentials();

            try
            {
                var retVal = this.m_client.CreateRole(new SanteDB.Core.Model.AMI.Auth.SecurityRoleInfo(roleInfo)
                {
                    Policies = roleInfo.Policies.Select(o => new SanteDB.Core.Model.AMI.Auth.SecurityPolicyInfo(o)).ToList()
                });
                retVal.Entity.Policies = retVal.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return retVal.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not create role", e);
            }
        }

        /// <summary>
        /// Create a user
        /// </summary>
        public SecurityUser CreateUser(SecurityUser userInfo, string password)
        {
            this.GetCredentials();

            try
            {
                var retVal = this.m_client.CreateUser(new SanteDB.Core.Model.AMI.Auth.SecurityUserInfo(userInfo)
                {
                    Roles = userInfo.Roles.Select(o => o.Name).ToList()
                });
                retVal.Entity.Roles = retVal.Roles.Select(o => this.GetRole(o)).ToList();
                return retVal.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not create user", e);
            }
        }

        /// <summary>
        /// Create a user entity
        /// </summary>
        public UserEntity CreateUserEntity(UserEntity userEntity)
        {
            this.GetCredentials();

            try
            {
                var hdsiClient = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                hdsiClient.Client.Credentials = this.m_client.Client.Credentials;
                return hdsiClient.Create(userEntity);
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not create user entity", e);
            }
        }

        /// <summary>
        /// Find applications
        /// </summary>
        public IEnumerable<SecurityApplication> FindApplications(Expression<Func<SecurityApplication, bool>> query)
        {
            int tr = 0;
            return this.FindApplications(query, 0, null, out tr);
        }

        /// <summary>
        /// Find applications
        /// </summary>
        public IEnumerable<SecurityApplication> FindApplications(Expression<Func<SecurityApplication, bool>> query, int offset, int? count, out int totalResults)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.Query(query, offset, count, out totalResults).CollectionItem.OfType<SecurityApplicationInfo>().Select(o =>
                {
                    o.Entity.Policies = o.Policies.Select(p => p.ToPolicyInstance()).ToList();
                    return o.Entity;
                });
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not query applications", e);
            }
        }

        /// <summary>
        /// Find devices
        /// </summary>
        public IEnumerable<SecurityDevice> FindDevices(Expression<Func<SecurityDevice, bool>> query)
        {
            int tr = 0;
            return this.FindDevices(query, 0, null, out tr);
        }

        /// <summary>
        /// Find devices
        /// </summary>
        public IEnumerable<SecurityDevice> FindDevices(Expression<Func<SecurityDevice, bool>> query, int offset, int? count, out int totalResults)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.Query(query, offset, count, out totalResults).CollectionItem.OfType<SecurityDeviceInfo>().Select(o =>
                {
                    o.Entity.Policies = o.Policies.Select(p => p.ToPolicyInstance()).ToList();
                    return o.Entity;
                });
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not query devices", e);
            }
        }

        /// <summary>
        /// Find policies
        /// </summary>
        public IEnumerable<SecurityPolicy> FindPolicies(Expression<Func<SecurityPolicy, bool>> query)
        {
            int tr = 0;
            return this.FindPolicies(query, 0, null, out tr);
        }

        /// <summary>
        /// Find policies
        /// </summary>
        public IEnumerable<SecurityPolicy> FindPolicies(Expression<Func<SecurityPolicy, bool>> query, int offset, int? count, out int totalResults)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.Query(query, offset, count, out totalResults).CollectionItem.OfType<SecurityPolicyInfo>().Select(o => o.Policy);
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not query devices", e);
            }
        }

        /// <summary>
        /// Find roles
        /// </summary>
        public IEnumerable<SecurityRole> FindRoles(Expression<Func<SecurityRole, bool>> query)
        {
            int tr = 0;
            return this.FindRoles(query, 0, null, out tr);
        }

        /// <summary>
        /// Find all roles
        /// </summary>
        public IEnumerable<SecurityRole> FindRoles(Expression<Func<SecurityRole, bool>> query, int offset, int? count, out int totalResults)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.Query(query, offset, count, out totalResults).CollectionItem.OfType<SecurityRoleInfo>().Select(o =>
                {
                    o.Entity.Policies = o.Policies.Select(p => p.ToPolicyInstance()).ToList();
                    return o.Entity;
                });
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not query roles", e);
            }
        }

        /// <summary>
        /// Find all user entities
        /// </summary>
        public IEnumerable<UserEntity> FindUserEntity(Expression<Func<UserEntity, bool>> expression)
        {
            int tr = 0;
            return this.FindUserEntity(expression, 0, null, out tr);
        }

        /// <summary>
        /// Find user entity
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="totalCount"></param>
        /// <returns></returns>
        public IEnumerable<UserEntity> FindUserEntity(Expression<Func<UserEntity, bool>> expression, int offset, int? count, out int totalCount)
        {
            this.GetCredentials();

            try
            {
                var hdsiClient = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                hdsiClient.Client.Credentials = this.m_client.Client.Credentials;
                var retVal = hdsiClient.Query<UserEntity>(expression, offset, count, false);
                totalCount = retVal.TotalResults;
                return retVal.Item.OfType<UserEntity>();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not get user entity", e);
            }
        }

        /// <summary>
        /// Find users
        /// </summary>
        public IEnumerable<SecurityUser> FindUsers(Expression<Func<SecurityUser, bool>> query)
        {
            int tr = 0;
            return this.FindUsers(query, 0, null, out tr);
        }

        public IEnumerable<SecurityUser> FindUsers(Expression<Func<SecurityUser, bool>> query, int offset, int? count, out int totalResults)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.Query(query, offset, count, out totalResults).CollectionItem.OfType<SecurityUserInfo>().Select(o =>
                {
                    o.Entity.Roles = o.Roles.Select(r => this.FindRoles(q => q.Name == r).FirstOrDefault()).ToList();
                    return o.Entity;
                });
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not query devices", e);
            }
        }

        /// <summary>
        /// Get active policies for the object
        /// </summary>
        public IEnumerable<SecurityPolicyInstance> GetActivePolicies(object securable)
        {
            return new AmiPolicyInformationService(this.m_cachedCredential as ClaimsPrincipal).GetActivePolicies(securable).Select(o => o.ToPolicyInstance());
        }

        /// <summary>
        /// Get all roles from the server
        /// </summary>
        public string[] GetAllRoles()
        {
            return this.FindRoles(o => o.ObsoletionTime == null).Select(o => o.Name).ToArray();
        }

        /// <summary>
        /// Get the specified application
        /// </summary>
        public SecurityApplication GetApplication(Guid applicationId)
        {
            this.GetCredentials();
            try
            {
                var application = this.m_client.GetApplication(applicationId);
                application.Entity.Policies = application.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return application.Entity;

            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve application", e);
            }
        }

        /// <summary>
        /// Get security device
        /// </summary>
        public SecurityDevice GetDevice(Guid deviceId)
        {
            this.GetCredentials();
            try
            {
                var device = this.m_client.GetDevice(deviceId);
                device.Entity.Policies = device.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return device.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve device", e);
            }
        }

        /// <summary>
        /// Get policy
        /// </summary>
        public SecurityPolicy GetPolicy(Guid policyId)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.GetPolicy(policyId)?.Policy;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve policy", e);
            }
        }

        /// <summary>
        /// Get policy by ID
        /// </summary>
        public SecurityPolicy GetPolicy(string policyOid)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.GetPolicies(o => o.Oid == policyOid).CollectionItem.OfType<SecurityPolicyInfo>().FirstOrDefault()?.Policy;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve policy", e);
            }
        }

        /// <summary>
        /// Get provenance object
        /// </summary>
        public SecurityProvenance GetProvenance(Guid provenanceId)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.GetProvenance(provenanceId);
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve security provenance information", e);
            }
        }

        /// <summary>
        /// Get a role
        /// </summary>
        public SecurityRole GetRole(Guid roleId)
        {
            this.GetCredentials();
            try
            {
                var role = this.m_client.GetRole(roleId);
                role.Entity.Policies = role.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return role.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve role", e);
            }
        }

        /// <summary>
        /// Get role by name
        /// </summary>
        public SecurityRole GetRole(string roleName)
        {
            this.GetCredentials();
            try
            {
                var role = this.m_client.GetRoles(r => r.Name == roleName).CollectionItem.OfType<SecurityRoleInfo>().FirstOrDefault();
                if (role != null)
                    role.Entity.Policies = role.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return role?.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve role", e);
            }
        }

        /// <summary>
        /// Get user by user name
        /// </summary>
        public SecurityUser GetUser(string userName)
        {
            this.GetCredentials();
            try
            {
                var user = this.m_client.GetUsers(u => u.UserName == userName).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault();
                if (user != null)
                    user.Entity.Roles = user.Roles.Select(r => this.GetRole(r)).ToList();
                return user?.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve user", e);
            }
        }

        /// <summary>
        /// Get user
        /// </summary>
        public SecurityUser GetUser(Guid userId)
        {
            this.GetCredentials();
            try
            {
                var user = this.m_client.GetUser(userId);
                if (user != null)
                    user.Entity.Roles = user.Roles.Select(r => this.GetRole(r)).ToList();
                return user?.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve user", e);
            }
        }

        /// <summary>
        /// Get user from identitiy
        /// </summary>
        public SecurityUser GetUser(IIdentity identity)
        {
            return this.GetUser(identity.Name);
        }

        /// <summary>
        /// Get user entity object
        /// </summary>
        public UserEntity GetUserEntity(Guid id, Guid versionId)
        {
            this.GetCredentials();

            try
            {
                var hdsiClient = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                hdsiClient.Client.Credentials = this.m_client.Client.Credentials;
                return hdsiClient.Get<UserEntity>(id, versionId != Guid.Empty ? (Guid?)versionId : null) as UserEntity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not get user entity", e);
            }
        }

        /// <summary>
        /// Get user entity by identity
        /// </summary>
        public UserEntity GetUserEntity(IIdentity identity)
        {
            this.GetCredentials();

            try
            {
                var hdsiClient = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                hdsiClient.Client.Credentials = this.m_client.Client.Credentials;
                return hdsiClient.Query<UserEntity>(o => o.SecurityUser.UserName == identity.Name, 0, 1, false).Item.FirstOrDefault() as UserEntity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not get user entity", e);
            }
        }

        /// <summary>
        /// Determine if user is in role
        /// </summary>
        public bool IsUserInRole(string userName, string role)
        {
            var user = this.GetUser(userName);
            return user.Roles.Any(o => o.Name == role);
        }

        /// <summary>
        /// Lock a user
        /// </summary>
        public void LockUser(Guid userId)
        {
            this.GetCredentials();
            try
            {
                this.m_client.LockUser(userId);
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not lock user", e);
            }
        }

        /// <summary>
        /// Obsolete an application
        /// </summary>
        public SecurityApplication ObsoleteApplication(Guid applicationId)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.DeleteApplication(applicationId).Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not delte application", e);
            }
        }

        /// <summary>
        /// Obsoletes a device
        /// </summary>
        public SecurityDevice ObsoleteDevice(Guid deviceId)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.DeleteDevice(deviceId).Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not delete device", e);
            }
        }

        /// <summary>
        /// Delete a policy
        /// </summary>
        public SecurityPolicy ObsoletePolicy(Guid policyId)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.DeletePolicy(policyId).Policy;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not delete policy", e);
            }
        }

        /// <summary>
        /// Obsolete role
        /// </summary>
        public SecurityRole ObsoleteRole(Guid roleId)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.DeleteRole(roleId).Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not delete role", e);
            }
        }

        /// <summary>
        /// Delete user
        /// </summary>
        public SecurityUser ObsoleteUser(Guid userId)
        {
            this.GetCredentials();
            try
            {
                return this.m_client.DeleteUser(userId).Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not delete device", e);
            }
        }

        /// <summary>
        /// Delete user entity
        /// </summary>
        public UserEntity ObsoleteUserEntity(Guid id)
        {
            this.GetCredentials();
            try
            {
                var hdsiClient = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                hdsiClient.Client.Credentials = this.m_client.Client.Credentials;
                return hdsiClient.Obsolete(new UserEntity() { Key = id }) as UserEntity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not delete user entity", e);
            }

        }

        /// <summary>
        /// Remove application from roles
        /// </summary>
        public void RemoveUsersFromRoles(string[] users, string[] roles)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Save application
        /// </summary>
        public SecurityApplication SaveApplication(SecurityApplication application)
        {
            this.GetCredentials();
            try
            {
                var retVal = this.m_client.UpdateApplication(application.Key.Value, new SecurityApplicationInfo(application)
                {
                    Policies = application.Policies.Select(o => new SecurityPolicyInfo(o)).ToList()
                });
                retVal.Entity.Policies = retVal.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return retVal.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error saving application", e);
            }
        }

        /// <summary>
        /// Save device
        /// </summary>
        public SecurityDevice SaveDevice(SecurityDevice device)
        {
            this.GetCredentials();
            try
            {
                var retVal = this.m_client.UpdateDevice(device.Key.Value, new SecurityDeviceInfo(device)
                {
                    Policies = device.Policies.Select(o => new SecurityPolicyInfo(o)).ToList()
                });
                retVal.Entity.Policies = retVal.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return retVal.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error saving application", e);
            }
        }

        /// <summary>
        /// Cannot update policies
        /// </summary>
        public SecurityPolicy SavePolicy(SecurityPolicy policy)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Save role 
        /// </summary>
        public SecurityRole SaveRole(SecurityRole role)
        {
            this.GetCredentials();
            try
            {
                var retVal = this.m_client.UpdateRole(role.Key.Value, new SecurityRoleInfo(role)
                {
                    Policies = role.Policies.Select(o => new SecurityPolicyInfo(o)).ToList()
                });
                retVal.Entity.Policies = retVal.Policies.Select(o => o.ToPolicyInstance()).ToList();
                return retVal.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error saving role", e);
            }
        }

        /// <summary>
        /// Save user
        /// </summary>
        public SecurityUser SaveUser(SecurityUser user)
        {
            this.GetCredentials();
            try
            {
                var retVal = this.m_client.UpdateUser(user.Key.Value, new SecurityUserInfo(user)
                {
                    Roles = user.Roles.Select(o => o.Name).ToList()
                });
                retVal.Entity.Roles = retVal.Roles.Select(o => this.GetRole(o)).ToList();
                return retVal.Entity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error saving role", e);
            }
        }

        /// <summary>
        /// Save user entity
        /// </summary>
        public UserEntity SaveUserEntity(UserEntity userEntity)
        {
            this.GetCredentials();
            try
            {
                var hdsiClient = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                hdsiClient.Client.Credentials = this.m_client.Client.Credentials;
                return hdsiClient.Update(userEntity) as UserEntity;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not update user entity", e);
            }
        }

        /// <summary>
        /// Unlock the user
        /// </summary>
        public void UnlockUser(Guid userId)
        {
            this.GetCredentials();
            try
            {
                this.m_client.UnlockUser(userId);
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not unlock user", e);
            }
        }

        /// <summary>
        /// Find user entity
        /// </summary>
        IEnumerable<UserEntity> IRepositoryService<UserEntity>.Find(Expression<Func<UserEntity, bool>> query)
        {
            return this.FindUserEntity(query);
        }

        /// <summary>
        /// Find user entity
        /// </summary>
        IEnumerable<UserEntity> IRepositoryService<UserEntity>.Find(Expression<Func<UserEntity, bool>> query, int offset, int? count, out int totalResults)
        {
            return this.FindUserEntity(query, offset, count, out totalResults);
        }

        /// <summary>
        /// Find the specified security application
        /// </summary>
        IEnumerable<SecurityApplication> IRepositoryService<SecurityApplication>.Find(Expression<Func<SecurityApplication, bool>> query)
        {
            return this.FindApplications(query);
        }

        /// <summary>
        /// Find the security application with the specified limiters
        /// </summary>
        /// <param name="query">The query to filter</param>
        /// <param name="offset">The offset of the first record</param>
        /// <param name="count">The number of records to return</param>
        /// <param name="totalResults">The total results </param>
        /// <returns>The matching security applications</returns>
        IEnumerable<SecurityApplication> IRepositoryService<SecurityApplication>.Find(Expression<Func<SecurityApplication, bool>> query, int offset, int? count, out int totalResults)
        {
            return this.FindApplications(query, offset, count, out totalResults);
        }

        /// <summary>
        /// Find security devices matching the query
        /// </summary>
        IEnumerable<SecurityDevice> IRepositoryService<SecurityDevice>.Find(Expression<Func<SecurityDevice, bool>> query)
        {
            return this.FindDevices(query);
        }

        /// <summary>
        /// Find security devices matching the query
        /// </summary>
        IEnumerable<SecurityDevice> IRepositoryService<SecurityDevice>.Find(Expression<Func<SecurityDevice, bool>> query, int offset, int? count, out int totalResults)
        {
            return this.FindDevices(query, offset, count, out totalResults);
        }

        /// <summary>
        /// Find the specified security roles
        /// </summary>
        IEnumerable<SecurityRole> IRepositoryService<SecurityRole>.Find(Expression<Func<SecurityRole, bool>> query)
        {
            return this.FindRoles(query);
        }

        /// <summary>
        /// Find specified security roles
        /// </summary>
        IEnumerable<SecurityRole> IRepositoryService<SecurityRole>.Find(Expression<Func<SecurityRole, bool>> query, int offset, int? count, out int totalResults)
        {
            return this.FindRoles(query, offset, count, out totalResults);
        }

        /// <summary>
        /// Find specified security users
        /// </summary>
        IEnumerable<SecurityUser> IRepositoryService<SecurityUser>.Find(Expression<Func<SecurityUser, bool>> query)
        {
            return this.FindUsers(query);
        }

        /// <summary>
        /// Find specified security users with limits
        /// </summary>
        IEnumerable<SecurityUser> IRepositoryService<SecurityUser>.Find(Expression<Func<SecurityUser, bool>> query, int offset, int? count, out int totalResults)
        {
            return this.FindUsers(query, offset, count, out totalResults);
        }

        /// <summary>
        /// Find security policy
        /// </summary>
        IEnumerable<SecurityPolicy> IRepositoryService<SecurityPolicy>.Find(Expression<Func<SecurityPolicy, bool>> query)
        {
            int tr = 0;
            return this.FindPolicies(query, 0, null, out tr);
        }

        /// <summary>
        /// Find policy
        /// </summary>
        IEnumerable<SecurityPolicy> IRepositoryService<SecurityPolicy>.Find(Expression<Func<SecurityPolicy, bool>> query, int offset, int? count, out int totalResults)
        {
            return this.FindPolicies(query, offset, count, out totalResults);
        }

        /// <summary>
        /// Get user entity
        /// </summary>
        UserEntity IRepositoryService<UserEntity>.Get(Guid key)
        {
            return this.GetUserEntity(key, Guid.Empty);
        }

        /// <summary>
        /// Get user entity
        /// </summary>
        UserEntity IRepositoryService<UserEntity>.Get(Guid key, Guid versionKey)
        {
            return this.GetUserEntity(key, versionKey);
        }

        /// <summary>
        /// Get specified security application
        /// </summary>
        SecurityApplication IRepositoryService<SecurityApplication>.Get(Guid key)
        {
            return this.GetApplication(key);
        }

        /// <summary>
        /// Get specified security application
        /// </summary>
        SecurityApplication IRepositoryService<SecurityApplication>.Get(Guid key, Guid versionKey)
        {
            return this.GetApplication(key);
        }

        /// <summary>
        /// Get security device
        /// </summary>
        SecurityDevice IRepositoryService<SecurityDevice>.Get(Guid key)
        {
            return this.GetDevice(key);
        }

        /// <summary>
        /// Get security device
        /// </summary>
        SecurityDevice IRepositoryService<SecurityDevice>.Get(Guid key, Guid versionKey)
        {
            return this.GetDevice(key);
        }

        /// <summary>
        /// Get the specified security role
        /// </summary> 
        SecurityRole IRepositoryService<SecurityRole>.Get(Guid key)
        {
            return this.GetRole(key);
        }

        /// <summary>
        /// Get specified security role
        /// </summary>
        SecurityRole IRepositoryService<SecurityRole>.Get(Guid key, Guid versionKey)
        {
            return this.GetRole(key);
        }

        /// <summary>
        /// Get the specified security user
        /// </summary>
        SecurityUser IRepositoryService<SecurityUser>.Get(Guid key)
        {
            return this.GetUser(key);
        }

        /// <summary>
        /// Get the specified security user
        /// </summary>
        SecurityUser IRepositoryService<SecurityUser>.Get(Guid key, Guid versionKey)
        {
            return this.GetUser(key);
        }

        /// <summary>
        /// Get security policy
        /// </summary>
        SecurityPolicy IRepositoryService<SecurityPolicy>.Get(Guid key)
        {
            return this.GetPolicy(key);

        }

        /// <summary>
        /// Get security policy
        /// </summary>
        SecurityPolicy IRepositoryService<SecurityPolicy>.Get(Guid key, Guid versionKey)
        {
            return this.GetPolicy(key);
        }

        /// <summary>
        /// Insert user entity
        /// </summary>
        UserEntity IRepositoryService<UserEntity>.Insert(UserEntity data)
        {
            return this.CreateUserEntity(data);
        }

        /// <summary>
        /// Insert specified user entity
        /// </summary>
        SecurityApplication IRepositoryService<SecurityApplication>.Insert(SecurityApplication data)
        {
            return this.CreateApplication(data);
        }

        /// <summary>
        /// Insert the specified security device
        /// </summary>
        SecurityDevice IRepositoryService<SecurityDevice>.Insert(SecurityDevice data)
        {
            return this.CreateDevice(data);
        }

        /// <summary>
        /// Insert the specified security role
        /// </summary>
        SecurityRole IRepositoryService<SecurityRole>.Insert(SecurityRole data)
        {
            return this.CreateRole(data);
        }

        /// <summary>
        /// Insert the specified security user
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        SecurityUser IRepositoryService<SecurityUser>.Insert(SecurityUser data)
        {
            return this.CreateUser(data, data.Password);
        }

        /// <summary>
        /// Insert
        /// </summary>
        SecurityPolicy IRepositoryService<SecurityPolicy>.Insert(SecurityPolicy data)
        {
            return this.CreatePolicy(data);
        }

        /// <summary>
        /// Obsolete
        /// </summary>
        UserEntity IRepositoryService<UserEntity>.Obsolete(Guid key)
        {
            return this.ObsoleteUserEntity(key);
        }

        /// <summary>
        /// Obsolete the security application
        /// </summary>
        SecurityApplication IRepositoryService<SecurityApplication>.Obsolete(Guid key)
        {
            return this.ObsoleteApplication(key);
        }

        /// <summary>
        /// OBsolete the device
        /// </summary>
        SecurityDevice IRepositoryService<SecurityDevice>.Obsolete(Guid key)
        {
            return this.ObsoleteDevice(key);
        }

        /// <summary>
        /// Obsolete role
        /// </summary>
        SecurityRole IRepositoryService<SecurityRole>.Obsolete(Guid key)
        {
            return this.ObsoleteRole(key);
        }

        /// <summary>
        /// Obsolete security user
        /// </summary>
        SecurityUser IRepositoryService<SecurityUser>.Obsolete(Guid key)
        {
            return this.ObsoleteUser(key);
        }

        /// <summary>
        /// Obsolete
        /// </summary>
        SecurityPolicy IRepositoryService<SecurityPolicy>.Obsolete(Guid key)
        {
            return this.ObsoletePolicy(key);
        }

        /// <summary>
        /// Save user entity
        /// </summary>
        UserEntity IRepositoryService<UserEntity>.Save(UserEntity data)
        {
            return this.SaveUserEntity(data);
        }

        /// <summary>
        /// Save the security application
        /// </summary>
        SecurityApplication IRepositoryService<SecurityApplication>.Save(SecurityApplication data)
        {
            return this.SaveApplication(data);
        }

        /// <summary>
        /// Save security device
        /// </summary>
        SecurityDevice IRepositoryService<SecurityDevice>.Save(SecurityDevice data)
        {
            return this.SaveDevice(data);
        }

        /// <summary>
        /// Save the security role
        /// </summary>
        SecurityRole IRepositoryService<SecurityRole>.Save(SecurityRole data)
        {
            return this.SaveRole(data);
        }

        /// <summary>
        /// Save the security user
        /// </summary>
        SecurityUser IRepositoryService<SecurityUser>.Save(SecurityUser data)
        {
            return this.SaveUser(data);
        }

        /// <summary>
        /// Save security policy
        /// </summary>
        SecurityPolicy IRepositoryService<SecurityPolicy>.Save(SecurityPolicy data)
        {
            return this.SavePolicy(data);
        }

        /// <summary>
        /// Get security provenance
        /// </summary>
        SecurityProvenance IRepositoryService<SecurityProvenance>.Get(Guid key)
        {
            return this.GetProvenance(key);
        }

        /// <summary>
        /// Get provenance
        /// </summary>
        SecurityProvenance IRepositoryService<SecurityProvenance>.Get(Guid key, Guid versionKey)
        {
            return this.GetProvenance(key);
        }

        IEnumerable<SecurityProvenance> IRepositoryService<SecurityProvenance>.Find(Expression<Func<SecurityProvenance, bool>> query)
        {
            throw new NotImplementedException();
        }

        IEnumerable<SecurityProvenance> IRepositoryService<SecurityProvenance>.Find(Expression<Func<SecurityProvenance, bool>> query, int offset, int? count, out int totalResults)
        {
            throw new NotImplementedException();
        }

        SecurityProvenance IRepositoryService<SecurityProvenance>.Insert(SecurityProvenance data)
        {
            throw new NotImplementedException();
        }

        SecurityProvenance IRepositoryService<SecurityProvenance>.Save(SecurityProvenance data)
        {
            throw new NotImplementedException();
        }

        SecurityProvenance IRepositoryService<SecurityProvenance>.Obsolete(Guid key)
        {
            throw new NotImplementedException();
        }
    }
}
