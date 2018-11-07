using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services
{
    /// <summary>
	/// Represents an identity service which authenticates devices.
	/// </summary>
	public interface IDeviceIdentityProviderService
    {
        /// <summary>
        /// Fired after an authentication request has been made.
        /// </summary>
        event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// Fired prior to an authentication request being made.
        /// </summary>
        event EventHandler<AuthenticatingEventArgs> Authenticating;

        /// <summary>
        /// Authenticates the specified device identifier.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="deviceSecret">The device secret.</param>
        /// <returns>Returns the authenticated device principal.</returns>
        IPrincipal Authenticate(string deviceId, string deviceSecret);
      
    }
}
