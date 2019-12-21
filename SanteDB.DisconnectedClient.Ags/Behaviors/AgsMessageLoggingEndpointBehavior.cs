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
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Xamarin.Threading;
using System;
using System.Collections.Generic;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    class AgsMessageLoggingEndpointBehavior : IEndpointBehavior, IMessageInspector
    {

        // Trace source name
        private Tracer m_traceSource = Tracer.GetTracer(typeof(AgsMessageLoggingEndpointBehavior));

        // Correlation id
        [ThreadStatic]
        private static KeyValuePair<Guid, DateTime> httpCorrelation;

        /// <summary>
        /// After receiving the request
        /// </summary>
        /// <param name="request"></param>
        public void AfterReceiveRequest(RestRequestMessage request)
        {
            Guid httpCorrelator = Guid.NewGuid();

            // Windows we get CPU usage
            var sdbHealth = ApplicationContext.Current.GetService<SanteDBThreadPool>();
            float usage = sdbHealth.ActiveThreads / (float)sdbHealth.Concurrency;

            this.m_traceSource.TraceVerbose("HTTP RQO {0} : {1} {2} ({3}) - {4} (CPU {5}%)",
                RestOperationContext.Current.IncomingRequest.RemoteEndPoint,
                request.Method,
                request.Url,
                RestOperationContext.Current.IncomingRequest.UserAgent,
                httpCorrelator,
                usage);

            httpCorrelation = new KeyValuePair<Guid, DateTime>(httpCorrelator, DateTime.Now);
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
            var processingTime = DateTime.Now.Subtract(httpCorrelation.Value);

            // Windows we get CPU usage
            var sdbHealth = ApplicationContext.Current.GetService<SanteDBThreadPool>();
            float usage = sdbHealth.ActiveThreads / (float)sdbHealth.Concurrency;

            this.m_traceSource.TraceVerbose("HTTP RSP {0} : {1} ({2} ms - CPU {3}%)",
                httpCorrelation.Key,
                response.StatusCode,
                processingTime.TotalMilliseconds,
                usage);
        }
    }
}
