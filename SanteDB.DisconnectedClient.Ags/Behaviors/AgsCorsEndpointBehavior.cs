using RestSrvr;
using RestSrvr.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    public class AgsCorsEndpointBehavior : IEndpointBehavior, IMessageInspector
    {

        private XElement m_configuration;

        /// <summary>
        /// Creates a new CorsEndpoint Behavior
        /// </summary>
        public AgsCorsEndpointBehavior(XElement configuration)
        {
            this.m_configuration = configuration;
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
        /// Before sending the respose
        /// </summary>
        /// <param name="response"></param>
        public void BeforeSendResponse(RestResponseMessage response)
        {
            var resourcePath = RestOperationContext.Current.IncomingRequest.Url.AbsolutePath.Substring(RestOperationContext.Current.ServiceEndpoint.Description.ListenUri.AbsolutePath.Length);
            var settings = this.m_configuration.Descendants().OfType<XElement>().FirstOrDefault(e => e.Attributes().Any(a => a.Name == "resource" && (a.Value == "*" || a.Value == resourcePath)));

            if (settings != null)
            {
                Dictionary<String, String> requiredHeaders = new Dictionary<string, string>() {
                    {"Access-Control-Allow-Origin", settings.Attributes().FirstOrDefault(o=>o.Name == "domain")?.Value ?? "*"},
                    {"Access-Control-Allow-Methods", String.Join(",", settings.Descendants().OfType<XElement>().Where(e=>e.Name == "action").Select(e=>e.Value))},
                    {"Access-Control-Allow-Headers", String.Join(",", settings.Descendants().OfType<XElement>().Where(e=>e.Name == "header").Select(e=>e.Value))}
                };
                foreach (var kv in requiredHeaders)
                    if (!RestOperationContext.Current.OutgoingResponse.Headers.AllKeys.Contains(kv.Key))
                        RestOperationContext.Current.OutgoingResponse.Headers.Add(kv.Key, kv.Value);
            }
        }
    }
}
