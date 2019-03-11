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
 * User: justi
 * Date: 2019-1-12
 */
using RestSrvr;
using RestSrvr.Message;
using System;
using System.Collections.Generic;
using System.Linq;
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
