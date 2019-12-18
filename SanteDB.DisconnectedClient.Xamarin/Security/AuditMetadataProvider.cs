using RestSrvr;
using SanteDB.Core.Auditing;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
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
                { AuditMetadataKey.CorrelationToken, RestOperationContext.Current?.Data["uuid"] }
            };
        }
    }
}
