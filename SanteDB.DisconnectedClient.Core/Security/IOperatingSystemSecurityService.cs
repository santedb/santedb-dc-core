using SanteDB.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security
{
    /// <summary>
    /// Permission types
    /// </summary>
    public enum PermissionType
    {
        /// <summary>
        /// The application is demanding permission to access geo-location services
        /// </summary>
        GeoLocation,
        /// <summary>
        /// The application is demanding permission to access the file system
        /// </summary>
        FileSystem,
        /// <summary>
        /// The application is demanding permission to access the camera
        /// </summary>
        Camera
    }

    /// <summary>
    /// Represents a security service for the operating system
    /// </summary>
    public interface IOperatingSystemSecurityService
    {

        /// <summary>
        /// True if the current execution context has the requested permission
        /// </summary>
        bool HasPermission(PermissionType permission);

        /// <summary>
        /// Request permission
        /// </summary>
        bool RequestPermission(PermissionType permission);
    }
}
