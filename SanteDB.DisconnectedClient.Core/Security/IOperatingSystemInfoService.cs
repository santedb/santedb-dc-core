﻿using SanteDB.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security
{
    /// <summary>
    /// Operating system information service
    /// </summary>
    public interface IOperatingSystemInfoService
    {

        /// <summary>
        /// Gets the version of the operating system
        /// </summary>
        String VersionString { get; }

        /// <summary>
        /// Gets the operating system id
        /// </summary>
        OperatingSystemID OperatingSystem { get; }

        /// <summary>
        /// Get the machine name
        /// </summary>
        String MachineName { get; }

        /// <summary>
        /// Get the manufacturer name
        /// </summary>
        String ManufacturerName { get; }
    }
}