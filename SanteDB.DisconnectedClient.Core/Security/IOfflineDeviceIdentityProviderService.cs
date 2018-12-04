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
    public interface IOfflineDeviceIdentityProviderService : IDeviceIdentityProviderService
    {

        /// <summary>
        /// Create an identity
        /// </summary>
        IIdentity CreateIdentity(Guid sid, string name, string deviceSecret, IPrincipal systemPrincipal);

        /// <summary>
        /// Change the device secret
        /// </summary>
        void ChangeSecret(string name, string deviceSecret, IPrincipal systemPrincipal);
    }
}
