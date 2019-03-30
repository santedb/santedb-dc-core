using SanteDB.Core;
using SanteDB.DisconnectedClient.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Services
{
    /// <summary>
    /// Represents the default implementation of the OSI service
    /// </summary>
    public class DefaultOperatingSystemInfoService : IOperatingSystemInfoService
    {
        /// <summary>
        /// Get the version of the OS
        /// </summary>
        public string VersionString => Environment.OSVersion.VersionString;

        /// <summary>
        /// Get the operating system id
        /// </summary>
        public OperatingSystemID OperatingSystem
        {
            get
            {
                switch(Environment.OSVersion.Platform)
                {
                    case PlatformID.MacOSX:
                        return OperatingSystemID.MacOS;
                    case PlatformID.Unix:
                        return OperatingSystemID.Linux;
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                    case PlatformID.Xbox:
                        return OperatingSystemID.Win32;
                    default:
                        return OperatingSystemID.Other;
                }
            }
        }

        /// <summary>
        /// Get the machine name
        /// </summary>
        public string MachineName => Environment.MachineName;

        /// <summary>
        /// Get manufacturer name
        /// </summary>
        public string ManufacturerName => "Generic Manufacturer";
    }
}
