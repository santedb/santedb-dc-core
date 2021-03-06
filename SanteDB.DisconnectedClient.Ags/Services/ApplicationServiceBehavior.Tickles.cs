﻿/*
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
using RestSrvr;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Tickler;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Application Services Interface Behavior
    /// </summary>
    public partial class ApplicationServiceBehavior
    {
        /// <summary>
        /// Create a new tickle (allows applications to tickle the user)
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public void CreateTickle(Tickle data)
        {
            ApplicationContext.Current.GetService<ITickleService>()?.SendTickle(data);
            RestOperationContext.Current.OutgoingResponse.StatusCode = 201;
        }

        /// <summary>
        /// Delete the tickle
        /// </summary>
        public void DeleteTickle(Guid id)
        {
            ApplicationContext.Current.GetService<ITickleService>()?.DismissTickle(id);
        }

        /// <summary>
        /// Get all tickles
        /// </summary>
        public List<Tickle> GetTickles()
        {
            var suser = ApplicationContext.Current.GetService<ISecurityRepositoryService>().GetUser(AuthenticationContext.Current.Principal.Identity);
            return ApplicationContext.Current.GetService<ITickleService>()?.GetTickles(o => o.Expiry > DateTime.Now && (o.Target == Guid.Empty || o.Target == suser.Key)).ToList();
        }

    }
}
