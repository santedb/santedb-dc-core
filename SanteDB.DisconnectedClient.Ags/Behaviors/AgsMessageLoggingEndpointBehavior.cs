using RestSrvr;
using RestSrvr.Message;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Xamarin.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
