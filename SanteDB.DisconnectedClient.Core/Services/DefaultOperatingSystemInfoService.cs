/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2019-11-27
 */
using SanteDB.Core;
using SanteDB.DisconnectedClient.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Services
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
