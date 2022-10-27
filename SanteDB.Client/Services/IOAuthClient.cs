using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Services
{
    public interface IOAuthClient
    {
        /// <summary>
        /// Authenticate a user givent an application and authenticated device principal.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="clientId"></param>
        /// <param name="devicePrincipal"></param>
        /// <returns></returns>
        IPrincipal AuthenticateUser(string username, string password, string clientId);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        /// <returns></returns>
        IPrincipal AuthenticateApp(string clientId, string clientSecret = null);
    }
}
