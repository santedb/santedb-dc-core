/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-11-23
 */
using RestSrvr;
using RestSrvr.Exceptions;
using RestSrvr.Message;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Security;
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
            if(error is PolicyViolationException)
            {
                var pve = error as PolicyViolationException;
                AuditUtil.AuditRestrictedFunction(error, uriMatched);
                if (pve.PolicyDecision == SanteDB.Core.Model.Security.PolicyGrantType.Elevate ||
                    pve.PolicyId == PermissionPolicyIdentifiers.Login)
                {
                    // Ask the user to elevate themselves
                    faultMessage.StatusCode = 401;
                    RestOperationContext.Current.OutgoingResponse.AddHeader("WWW-Authenticate", $"{RestOperationContext.Current.IncomingRequest.Url.Host} realm=\"{RestOperationContext.Current.IncomingRequest.Url.Host}\" error_code=\"insufficient_scope\" scope=\"{pve.PolicyId}\"");

                }
                else
                    faultMessage.StatusCode = 403;
            }
            else if (error is SecurityTokenException)
            {
                // TODO: Audit this
                faultMessage.StatusCode = (int)System.Net.HttpStatusCode.Unauthorized;
                RestOperationContext.Current.OutgoingResponse.AddHeader("WWW-Authenticate", $"Bearer realm=\"{RestOperationContext.Current.IncomingRequest.Url.Host}\" error=\"invalid_token\" error_description=\"{error.Message}\"");
            }
            else if (error is SecurityException)
            {
                AuditUtil.AuditRestrictedFunction(error, uriMatched);
                faultMessage.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
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
