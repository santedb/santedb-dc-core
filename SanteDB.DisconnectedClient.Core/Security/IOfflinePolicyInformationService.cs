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
    /// Represents an offline policy information service
    /// </summary>
    public interface IOfflinePolicyInformationService : IPolicyInformationService
    {
        /// <summary>
        /// Create a local offline policy
        /// </summary>
        void CreatePolicy(IPolicy policy, IPrincipal principal);
    }
}
