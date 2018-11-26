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
    /// Represents an offline identity provider service
    /// </summary>
    public interface IOfflineIdentityProviderService : IIdentityProviderService
    {
        /// <summary>
        /// Create a local offline identity
        /// </summary>
        IIdentity CreateIdentity(Guid sid, string username, string password, IPrincipal principal);

    }
}
