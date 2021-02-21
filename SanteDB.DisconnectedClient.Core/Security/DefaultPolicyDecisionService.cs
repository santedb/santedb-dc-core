/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents the policy decision service
    /// </summary>
    [Obsolete("Use SanteDB.Core.Security.DefaultPolicyDecisionService", true)]
    public class DefaultPolicyDecisionService : SanteDB.Core.Security.DefaultPolicyDecisionService
    {

        /// <summary>
        /// Creates cache service
        /// </summary>
        public DefaultPolicyDecisionService(IPasswordHashingService hashService, IAdhocCacheService cacheService = null) : base(hashService, cacheService)
        {

        }
    }
}