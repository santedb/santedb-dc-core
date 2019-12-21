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
using RestSrvr;
using RestSrvr.Message;
using SharpCompress.IO;
using SharpCompress.Compressors.Deflate;
using System;
using System.IO;

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
            if (!String.IsNullOrEmpty(compressionScheme) && compressionScheme.Contains("deflate") && response.Body != null)
            {
                var ms = new MemoryStream();
                using (var dfz = new DeflateStream(new NonDisposingStream(ms), SharpCompress.Compressors.CompressionMode.Compress))
                    response.Body.CopyTo(dfz);
                ms.Seek(0, SeekOrigin.Begin);
                response.Body.Dispose();
                response.Body = ms;
                response.Headers.Add("Content-Encoding", "deflate");
            }
        }
    }
}
