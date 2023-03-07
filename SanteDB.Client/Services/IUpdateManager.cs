using SanteDB.Core.Applets.Model;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Services
{
    /// <summary>
    /// Update manager service is responsible for checking for updates and downloading / applying them
    /// </summary>
    public interface IUpdateManager : IServiceImplementation
    {

        /// <summary>
        /// Get server version of a package
        /// </summary>
        AppletInfo GetServerInfo(String packageId);

        /// <summary>
        /// Install the specified package from the server version
        /// </summary>
        void Install(String packageId);

        /// <summary>
        /// Update all apps
        /// </summary>
        /// <param name="nonInteractive">True if the update should just not prompt for installation</param>
        void Update(bool nonInteractive);
    }
}
