using RestSrvr;
using RestSrvr.Exceptions;
using RestSrvr.Message;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.DisconnectedClient.Ags.Formatter;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using SanteDB.DisconnectedClient.Xamarin.Exceptions;
using SanteDB.Rest.Common.Fault;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// AGS ErrorHandler 
    /// </summary>
    public class AgsErrorHandlerServiceBehavior : IServiceBehavior, IServiceErrorHandler
    {
        // Error tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AgsErrorHandlerServiceBehavior));

        /// <summary>
        /// Apply the service behavior
        /// </summary>
        public void ApplyServiceBehavior(RestService service, ServiceDispatcher dispatcher)
        {
            dispatcher.ErrorHandlers.Clear();
            dispatcher.ErrorHandlers.Add(this);
        }

        /// <summary>
        /// Handle the error
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool HandleError(Exception error)
        {
            return true;
        }

        /// <summary>
        /// Provide the fault
        /// </summary>
        /// <param name="error"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public bool ProvideFault(Exception error, RestResponseMessage faultMessage)
        {
#if DEBUG
            var ie = error;
            while (ie != null)
            {
                this.m_tracer.TraceError("{0} - ({1}){2} - {3}", error == ie ? "" : "Caused By", 
                    RestOperationContext.Current.EndpointOperation?.Description.InvokeMethod.Name, 
                    ie.GetType().FullName, ie.Message);
                ie = ie.InnerException;
            }
#else
            if (error is TargetInvocationException)
                this.m_tracer.TraceError("{0} - {1} / {2}", RestOperationContext.Current.EndpointOperation.Description.InvokeMethod.Name, error.Message, error.InnerException?.Message);
            else
                this.m_tracer.TraceError("{0} - {1}", RestOperationContext.Current.EndpointOperation.Description.InvokeMethod.Name, error.Message);
#endif

            var uriMatched = RestOperationContext.Current.IncomingRequest.Url;

            object fault = new RestServiceFault(error);

            // Formulate appropriate response
            if (error is PolicyViolationException || error is SecurityException)
            {
                AuditUtil.AuditRestrictedFunction(error, uriMatched);
                faultMessage.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
            }
            else if (error is SecurityTokenException)
            {
                // TODO: Audit this
                faultMessage.StatusCode = (int)System.Net.HttpStatusCode.Unauthorized;
                faultMessage.Headers.Add("WWW-Authenticate", "Bearer");
            }
            else if (error is LimitExceededException)
            {
                faultMessage.StatusCode = (int)429;
                faultMessage.StatusDescription = "Too Many Requests";
                faultMessage.Headers.Add("Retry-After", "1200");
            }
            else if (error is UnauthorizedAccessException)
            {
                AuditUtil.AuditRestrictedFunction(error, uriMatched);
                faultMessage.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
            }
            else if (error is FaultException)
            {
                faultMessage.StatusCode = (int)(error as FaultException).StatusCode;
                if (error.GetType() != typeof(FaultException)) // Special classification
                    fault = error.GetType().GetRuntimeProperty("Body").GetValue(error);
            }
            else if (error is Newtonsoft.Json.JsonException ||
                error is System.Xml.XmlException)
                faultMessage.StatusCode = (int)400;
            else if (error is DuplicateKeyException || error is DuplicateNameException)
                faultMessage.StatusCode = (int)409;
            else if (error is FileNotFoundException || error is KeyNotFoundException)
                faultMessage.StatusCode = (int)404;
            else if (error is DetectedIssueException)
                faultMessage.StatusCode = (int)422;
            else if (error is NotImplementedException)
                faultMessage.StatusCode = (int)501;
            else if (error is NotSupportedException)
                faultMessage.StatusCode = (int)405;
            else
                faultMessage.StatusCode = (int)500;

            AgsMessageDispatchFormatter.CreateFormatter(RestOperationContext.Current.ServiceEndpoint.Description.Contract.Type).SerializeResponse(faultMessage, null, fault);
            return true;
        }
    }
}
