using RestSrvr;
using RestSrvr.Message;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Linq;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Cache behavior
    /// </summary>
    public class AgsCacheBehavior : IEndpointBehavior, IMessageInspector
    {

        // Settings
        private readonly String[] cacheExtensions;


        /// <summary>
        /// Default options
        /// </summary>
        public AgsCacheBehavior() : this(null)
        {
        }

        /// <summary>
        /// CORS endpoint behavior as configured from endpoint behavior
        /// </summary>
        public AgsCacheBehavior(XElement xe)
        {
            if (xe == null)
                cacheExtensions = new string[] { ".css", ".js", ".json", ".png", ".jpg", ".woff2", ".ttf" };
            else
                cacheExtensions = xe.Elements((XNamespace)"http://santedb.org/configuration" + "extension").Select(o => o.Value).ToArray();

        }

        /// <summary>
        /// After receive request
        /// </summary>
        public void AfterReceiveRequest(RestRequestMessage request)
        {
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

            var ext = RestOperationContext.Current.IncomingRequest.Url.AbsolutePath;
            if (ext.Contains("."))
            {
                ext = ext.Substring(ext.LastIndexOf("."));
                if (this.cacheExtensions.Contains(ext))
                {
                    RestOperationContext.Current.OutgoingResponse.AddHeader("Cache-Control", "public, max-age=7200");
                    RestOperationContext.Current.OutgoingResponse.AddHeader("Expires", DateTime.UtcNow.AddHours(1).ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'"));
                }
                else
                    RestOperationContext.Current.OutgoingResponse.AddHeader("Cache-Control", "no-cache");
            }
        }
    }
}
