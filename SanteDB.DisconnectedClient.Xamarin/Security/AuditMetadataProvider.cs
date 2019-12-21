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
 * Date: 2019-12-18
 */
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Auditing;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Security
{
    /// <summary>
    /// Audit metadata provider
    /// </summary>
    public class AuditMetadataProvider : IAuditMetadataProvider
    {
        /// <summary>
        /// Gets metadata for all audits
        /// </summary>
        public IDictionary<AuditMetadataKey, object> GetMetadata()
        {
            return new Dictionary<AuditMetadataKey, object>()
            {
                { AuditMetadataKey.PID, Process.GetCurrentProcess().Id },
                { AuditMetadataKey.ProcessName, Process.GetCurrentProcess().ProcessName },
                { AuditMetadataKey.SessionId, (AuthenticationContext.Current.Principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.SanteDBSessionIdClaim)?.Value },
                { AuditMetadataKey.CorrelationToken, RestOperationContext.Current?.Data["uuid"] },
                { AuditMetadataKey.AuditSourceType, "EndUserInterface" },
                { AuditMetadataKey.LocalEndpoint, RestOperationContext.Current?.IncomingRequest.Url },
                { AuditMetadataKey.RemoteHost, ApplicationServiceContext.Current.GetService<IRemoteEndpointResolver>()?.GetRemoteEndpoint() }
            };
        }
    }
}
