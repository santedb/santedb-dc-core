using SanteDB.Core.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Metadata
{
    /// <summary>
    /// Allows AGS services to be discovered by the metadata exchanger
    /// </summary>
    public class ApiEndpointProviderShim : IApiEndpointProvider
    {
        /// <summary>
        /// Gets the type of API
        /// </summary>
        public ServiceEndpointType ApiType { get; }

        /// <summary>
        /// Gets the url at which this is operating
        /// </summary>
        public string[] Url { get; }

        /// <summary>
        /// Gets the capabilities of this endpoint
        /// </summary>
        public ServiceEndpointCapabilities Capabilities { get; }

        /// <summary>
        /// Gets the behavior type
        /// </summary>
        public Type BehaviorType { get; }

        /// <summary>
        /// Creates a new api endpoint behavior
        /// </summary>
        public ApiEndpointProviderShim(Type behavior, ServiceEndpointType apiType, String url, ServiceEndpointCapabilities capabilities)
        {
            this.ApiType = apiType;
            this.Url = new string[] { url };
            this.Capabilities = capabilities;
            this.BehaviorType = behavior;
        }
    }
}
