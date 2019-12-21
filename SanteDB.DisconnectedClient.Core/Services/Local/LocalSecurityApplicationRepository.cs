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
 * User: Justin Fyfe
 * Date: 2019-8-8
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
    /// Local security application repository
    /// </summary>
    public class LocalSecurityApplicationRepository : GenericLocalSecurityRepository<SecurityApplication>
    {
        protected override string WritePolicy => PermissionPolicyIdentifiers.CreateApplication;
        protected override string DeletePolicy => PermissionPolicyIdentifiers.CreateApplication;
        protected override string AlterPolicy => PermissionPolicyIdentifiers.CreateApplication;


        /// <summary>
        /// Insert the device
        /// </summary>
        public override SecurityApplication Insert(SecurityApplication data)
        {
            if (!String.IsNullOrEmpty(data.ApplicationSecret))
                data.ApplicationSecret = ApplicationServiceContext.Current.GetService<IPasswordHashingService>().ComputeHash(data.ApplicationSecret);
            return base.Insert(data);
        }

        /// <summary>
        /// Save the security device
        /// </summary>
        public override SecurityApplication Save(SecurityApplication data)
        {
            if (!String.IsNullOrEmpty(data.ApplicationSecret))
                data.ApplicationSecret = ApplicationServiceContext.Current.GetService<IPasswordHashingService>().ComputeHash(data.ApplicationSecret);
            return base.Save(data);
        }
    }
}
