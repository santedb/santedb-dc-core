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
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// The bridged role provider is the preferred service for the role provider and is just a wrapper for <see cref="ILocalServiceProvider{IRoleProviderService}"/> 
    /// for the dCDR
    /// </summary>
    [PreferredService(typeof(IRoleProviderService))]
    public class BridgedRoleProvider : IRoleProviderService
    {
        // Local role provider service
        private readonly IRoleProviderService m_localRoleProvider;

        /// <summary>
        /// DI ctor
        /// </summary>
        public BridgedRoleProvider(ILocalServiceProvider<IRoleProviderService> roleProviderService)
        {
            this.m_localRoleProvider = roleProviderService.LocalProvider;
        }

        /// <inheritdoc/>
        public string ServiceName => "Bridged Role Provider";

        /// <inheritdoc/>
        public void AddUsersToRoles(string[] users, string[] roles, IPrincipal principal) => this.m_localRoleProvider.AddUsersToRoles(users, roles, principal);

        /// <inheritdoc/>
        public void CreateRole(string roleName, IPrincipal principal) => this.m_localRoleProvider.CreateRole(roleName, principal);

        /// <inheritdoc/>
        public string[] FindUsersInRole(string role) => this.m_localRoleProvider.FindUsersInRole(role);

        /// <inheritdoc/>
        public string[] GetAllRoles() => this.m_localRoleProvider.GetAllRoles();

        /// <inheritdoc/>
        public string[] GetAllRoles(string userName) => this.m_localRoleProvider.GetAllRoles(userName);

        /// <inheritdoc/>
        public bool IsUserInRole(string userName, string roleName) => this.m_localRoleProvider.IsUserInRole(userName, roleName);

        /// <inheritdoc/>
        public void RemoveUsersFromRoles(string[] users, string[] roles, IPrincipal principal) => this.m_localRoleProvider.RemoveUsersFromRoles(users, roles, principal);
    }
}
