/*
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
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security
{

    /// <summary>
    /// Represents an offline role provider service
    /// </summary>
    public interface IOfflineRoleProviderService : IRoleProviderService
    {
        /// <summary>
        /// Create offline role
        /// </summary>
        void CreateRole(string value, IPrincipal principal);

        /// <summary>
        /// Add specified policies to the specified roles
        /// </summary>
        void AddPoliciesToRoles(IPolicyInstance[] policies, string[] roles, IPrincipal principal);
    }
}
