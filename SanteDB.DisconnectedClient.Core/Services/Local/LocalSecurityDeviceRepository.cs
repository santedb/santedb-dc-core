﻿/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using SanteDB.Core;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Local security device repository
    /// </summary>
    public class LocalSecurityDeviceRepository : GenericLocalSecurityRepository<SecurityDevice>
    {
        protected override string WritePolicy => PermissionPolicyIdentifiers.CreateDevice;
        protected override string DeletePolicy => PermissionPolicyIdentifiers.CreateDevice;
        protected override string AlterPolicy => PermissionPolicyIdentifiers.CreateDevice;

        /// <summary>
        /// Insert the device
        /// </summary>
        public override SecurityDevice Insert(SecurityDevice data)
        {
            if (!String.IsNullOrEmpty(data.DeviceSecret))
                data.DeviceSecret = ApplicationServiceContext.Current.GetService<IPasswordHashingService>().ComputeHash(data.DeviceSecret);
            return base.Insert(data);
        }

        /// <summary>
        /// Save the security device
        /// </summary>
        public override SecurityDevice Save(SecurityDevice data)
        {
            if (!String.IsNullOrEmpty(data.DeviceSecret))
                data.DeviceSecret = ApplicationServiceContext.Current.GetService<IPasswordHashingService>().ComputeHash(data.DeviceSecret);
            return base.Save(data);
        }
    }
}
