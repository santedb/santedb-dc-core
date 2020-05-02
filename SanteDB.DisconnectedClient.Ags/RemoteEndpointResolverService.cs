using RestSrvr;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Ags
{
    /// <summary>
    /// Remote endpoint resolver
    /// </summary>
    public class RemoteEndpointResolverService : IRemoteEndpointResolver
    {
        /// <summary>
        /// Remote endpoint resolver
        /// </summary>
        public string ServiceName => "Default Remote Endpoint Resolver";

        /// <summary>
        /// Retrieve the remote endpoint information
        /// </summary>
        /// <returns></returns>
        public string GetRemoteEndpoint()
        {
            var fwdHeader = RestOperationContext.Current?.IncomingRequest.Headers["X-Forwarded-For"];
            if (!String.IsNullOrEmpty(fwdHeader))
                return fwdHeader;
            return RestOperationContext.Current?.IncomingRequest.RemoteEndPoint.Address.ToString();
        }

        /// <summary>
        /// Gets the URL that was originally requested
        /// </summary>
        public string GetRemoteRequestUrl()
        {
            return RestOperationContext.Current?.IncomingRequest.Url.ToString();

        }
    }
}
