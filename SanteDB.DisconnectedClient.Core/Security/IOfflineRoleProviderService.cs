using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security
{

    /// <summary>
    /// Represents an offline role provider service
    /// </summary>
    public interface IOfflineRoleProviderService : IRoleProviderService
    {
        /// <summary>
        /// Create offline role
        /// </summary>
        void CreateRole(string value, IPrincipal principal);

        /// <summary>
        /// Add specified policies to the specified roles
        /// </summary>
        void AddPoliciesToRoles(IPolicyInstance[] policies, string[] roles, IPrincipal principal);
    }
}
