using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Services
{
    public interface IOAuthClient
    {
        /// <summary>
        /// Authenticate a user given an authenticated device principal and optionally a specific application.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="clientId">Optional client_id to provide in the request. If this value is <c>null</c>, the Realm client_id will be used.</param>
        /// <param name="devicePrincipal"></param>
        /// <returns></returns>
        IClaimsPrincipal AuthenticateUser(string username, string password, string clientId = null);

        /// <summary>
        /// Authenticate an application given an authenticated device principal.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        /// <returns></returns>
        IClaimsPrincipal AuthenticateApp(string clientId, string clientSecret = null);

        IClaimsPrincipal Refresh(string refreshToken);
    }
}
