using RestSrvr;
using RestSrvr.Message;
using SharpCompress.Compressors.Deflate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Represents a behavior that compresses messages
    /// </summary>
    public class AgsCompressionEndpointBehavior : IEndpointBehavior, IMessageInspector
    {
        /// <summary>
        /// After receiving a request
        /// </summary>
        public void AfterReceiveRequest(RestRequestMessage request)
        {
            // Receiving compressed messages is not permitted on MINIMS
        }

        /// <summary>
        /// Apply the endpoint behavior
        /// </summary>
        public void ApplyEndpointBehavior(ServiceEndpoint endpoint, EndpointDispatcher dispatcher)
        {
            dispatcher.MessageInspectors.Add(this);
        }

        /// <summary>
        /// Before sending the response
        /// </summary>
        public void BeforeSendResponse(RestResponseMessage response)
        {
            var compressionScheme = RestOperationContext.Current.IncomingRequest.Headers["Accept-Encoding"];

            // Compress the body
            if(!String.IsNullOrEmpty(compressionScheme) && compressionScheme.Contains("deflate"))
            {
                var ms = new MemoryStream();
                using (var dfz = new DeflateStream(ms, SharpCompress.Compressors.CompressionMode.Compress, leaveOpen: true))
                    response.Body.CopyTo(dfz);
                ms.Seek(0, SeekOrigin.Begin);
                response.Body.Dispose();
                response.Body = ms;
                response.Headers.Add("Content-Encoding", "deflate");
            }
        }
    }
}
