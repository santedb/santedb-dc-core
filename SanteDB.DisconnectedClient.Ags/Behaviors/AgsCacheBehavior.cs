/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
using RestSrvr;
using RestSrvr.Message;
using System;
using System.Linq;
using System.Xml.Linq;

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
                    RestOperationContext.Current.OutgoingResponse.AddHeader("Cache-Control", "public, max-age=28800");
                    RestOperationContext.Current.OutgoingResponse.AddHeader("Expires", DateTime.UtcNow.AddHours(1).ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'"));
                }
                else
                    RestOperationContext.Current.OutgoingResponse.AddHeader("Cache-Control", "no-cache");
            }
        }
    }
}
