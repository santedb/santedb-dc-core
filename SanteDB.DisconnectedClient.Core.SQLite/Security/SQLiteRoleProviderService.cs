﻿/*
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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.SQLite.Security
{
    /// <summary>
    /// Local role provider service
    /// </summary>
    public class SQLiteRoleProviderService : IOfflineRoleProviderService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "SQLite Role Provider Service";

        // Configuration
        private DcDataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>();

        // Local tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteRoleProviderService));


        /// <summary>
        /// Add specified roles to the specified groups
        /// </summary>
        public void AddPoliciesToRoles(IPolicyInstance[] policyInstance, string[] roles, IPrincipal principal = null)
        {
            // Demand local admin
            try
            {
                ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, principal);


                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    foreach (var rn in roles)
                    {
                        try
                        {
                            conn.BeginTransaction();
                            var dbr = conn.Table<DbSecurityRole>().FirstOrDefault(o => o.Name == rn);
                            if (dbr == null)
                                throw new KeyNotFoundException(String.Format("Role {0} not found", rn));
                            var currentPolicies = conn.Query<DbSecurityPolicy>("select security_policy.* from security_policy inner join security_role_policy on (security_policy.uuid = security_role_policy.policy_id) where security_role_policy.role_id = ?", dbr.Uuid).ToList();
                            var toBeInserted = policyInstance.Where(o => !currentPolicies.Any(p => p.Oid == o.Policy.Oid)).Select(
                                o => new DbSecurityRolePolicy()
                                {
                                    GrantType = (int)o.Rule,
                                    PolicyId = conn.Table<DbSecurityPolicy>().Where(p => p.Oid == o.Policy.Oid).First().Uuid,
                                    RoleId = dbr.Uuid,
                                    Key = Guid.NewGuid()
                                }).ToList();
                            foreach (var itm in toBeInserted)
                                conn.Insert(itm);

                            conn.Commit();
                        }
                        catch (Exception e)
                        {
                            this.m_tracer.TraceError("Error assigning policies to role {0}: {1}", rn, e);
                            conn.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error adding policies {0} to role {1}", String.Join(",", policyInstance.Select(o => o.Policy.Oid)), String.Join(",", roles));
                throw new DataPersistenceException($"Error adding policies {String.Join(",", policyInstance.Select(o => o.Policy.Oid))} to role {String.Join(",", roles)}", e);
            }
        }

        /// <summary>
        /// Add the specified users to the specified roles
        /// </summary>
        public void AddUsersToRoles(string[] userNames, string[] roleNames, IPrincipal principal)
        {
            try
            {
                if (userNames == null)
                    throw new ArgumentNullException(nameof(userNames));
                if (roleNames == null)
                    throw new ArgumentNullException(nameof(roleNames));

                // Demand local admin
                ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, principal);

                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    foreach (var userName in userNames)
                    {
                        var un = userName.ToLower();
                        var dbu = conn.Table<DbSecurityUser>().FirstOrDefault(o => o.UserName.ToLower() == un);
                        if (dbu == null)
                            throw new KeyNotFoundException(String.Format("User {0} not found", un));
                        foreach (var rn in roleNames)
                        {
                            var dbr = conn.Table<DbSecurityRole>().FirstOrDefault(o => o.Name.ToLower() == rn.ToLower());
                            if (dbr == null)
                                throw new KeyNotFoundException(String.Format("Role {0} not found", rn));
                            else if (conn.Table<DbSecurityUserRole>().Where(o => o.RoleUuid == dbr.Uuid && o.UserUuid == dbu.Uuid).Count() == 0)
                                conn.Insert(new DbSecurityUserRole()
                                {
                                    RoleUuid = dbr.Uuid,
                                    UserUuid = dbu.Uuid,
                                    Key = Guid.NewGuid()
                                });
                        }

                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error adding users {0} to role {1}", String.Join(",", userNames), String.Join(",", roleNames));
                throw new DataPersistenceException($"Error adding users {String.Join(",", userNames)} to role {String.Join(",", roleNames)}", e);
            }
        }

        /// <summary>
        /// Create the specified role
        /// </summary>
        public void CreateRole(string value, IPrincipal principal)
        {
            try
            {
                // Demand local admin
                ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, principal);


                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                        var pk = Guid.NewGuid();
                        conn.Insert(new DbSecurityRole() { Name = value, Key = pk });
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Unable to create role: {e}");
                throw new DataPersistenceException($"Unable to create role {value}", e);
            }
        }

        /// <summary>
        /// Finds users in the specified role
        /// </summary>
        public string[] FindUsersInRole(string role)
        {
            if (role == null)
                throw new ArgumentNullException(nameof(role));
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                return conn.Query<DbSecurityUser>("SELECT security_user.* FROM security_user_role INNER JOIN security_user ON (security_user.uuid = security_user_role.user_Id) INNER JOIN security_role ON (security_user_role.role_Id = security_role.uuid) WHERE security_role.name = ?", role)
                            .Select(o => o.UserName)
                            .ToArray();
            }
        }

        /// <summary>
        /// Gets all roles
        /// </summary>
        public string[] GetAllRoles()
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                return conn.Table<DbSecurityRole>().ToList().Select(o => o.Name).ToArray();
            }
        }

        /// <summary>
        /// Get all roles for the specified user
        /// </summary>
        public string[] GetAllRoles(string userName)
        {
            if (userName == null)
                throw new ArgumentNullException(nameof(userName));

            var conn = this.CreateReadonlyConnection();
            using (conn.Lock())
            {
                return conn.Query<DbSecurityRole>("SELECT security_role.* FROM security_user_role INNER JOIN security_role ON (security_role.uuid = security_user_role.role_id) INNER JOIN security_user ON (security_user.uuid = security_user_role.user_id) WHERE lower(security_user.username) = lower(?)", userName)
                    .Select(p => p.Name)
                    .ToArray();
            }
        }

        /// <summary>
        /// Determine if the user in the role
        /// </summary>
        public bool IsUserInRole(string userName, string roleName)
        {
            var conn = this.CreateReadonlyConnection();
            using (conn.Lock())
                return conn.ExecuteScalar<Int32>("SELECT 1 FROM security_user_role INNER JOIN security_user ON(security_user.uuid = security_user_role.user_id) INNER JOIN security_role ON(security_role.uuid = security_user_role.role_id) WHERE security_user.username = ? AND security_role.name = ?", userName, roleName) > 0;
        }

        /// <summary>
        /// Determine if the principle in the role
        /// </summary>
        public bool IsUserInRole(IPrincipal principal, string roleName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove users from roles
        /// </summary>
        public void RemoveUsersFromRoles(string[] userNames, string[] roleNames, IPrincipal principal)
        {
            try
            {
                if (userNames == null)
                    throw new ArgumentNullException(nameof(userNames));
                if (roleNames == null)
                    throw new ArgumentNullException(nameof(roleNames));

                // Demand local admin
                ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, principal);


                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    foreach (var userName in userNames)
                    {
                        var un = userName.ToLower();
                        var dbu = conn.Table<DbSecurityUser>().FirstOrDefault(o => o.UserName.ToLower() == un);
                        if (dbu == null)
                            throw new KeyNotFoundException(String.Format("User {0} not found", un));
                        foreach (var rn in roleNames)
                        {
                            var dbr = conn.Table<DbSecurityRole>().FirstOrDefault(o => o.Name.ToLower() == rn.ToLower());
                            if (dbr == null)
                                throw new KeyNotFoundException(String.Format("Role {0} not found", rn));
                            conn.Table<DbSecurityUserRole>().Delete(o => o.RoleUuid == dbr.Uuid && o.UserUuid == dbu.Uuid);
                        }

                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error adding users {0} to role {1}", String.Join(",", userNames), String.Join(",", roleNames));
                throw new DataPersistenceException($"Error adding users {String.Join(",", userNames)} to role {String.Join(",", roleNames)}", e);
            }
        }

        /// <summary>
        /// Creates a connection to the local database
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateConnection()
        {
            return SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(this.m_configuration.MainDataSourceConnectionStringName));
        }

        /// <summary>
        /// Creates a connection to the local database
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateReadonlyConnection()
        {
            return SQLiteConnectionManager.Current.GetReadonlyConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(this.m_configuration.MainDataSourceConnectionStringName));
        }
    }
}