using SanteDB.DisconnectedClient.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security
{
    /// <summary>
    /// Represents an authentication service which can authenticate a user using a PIN which is a local secret for this device only
    /// </summary>
    public interface IPinAuthenticationService : IIdentityProviderService
    {

        /// <summary>
        /// Authenticate with a numeric PIN
        /// </summary>
        /// <param name="username">The user being authenticated</param>
        /// <param name="pin">The PIN number digits</param>
        /// <returns>The authenticated principal</returns>
        IPrincipal Authenticate(String username, byte[] pin);

        /// <summary>
        /// Authenticate with a numeric PIN
        /// </summary>
        /// <param name="principal">The user being authenticated</param>
        /// <param name="pin">The PIN number digits</param>
        /// <returns>The authenticated principal</returns>
        IPrincipal Authenticate(IPrincipal principal, byte[] pin);

        /// <summary>
        /// Change the user's PIN number
        /// </summary>
        /// <param name="userName">The name of the user to change PIN for</param>
        /// <param name="pin">The PIN to change to</param>
        void ChangePin(String userName, byte[] pin);
    }
}
