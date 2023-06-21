using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Security.Principal;
using System.Text;

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
