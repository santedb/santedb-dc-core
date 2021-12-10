/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */

using RestSrvr;
using RestSrvr.Exceptions;
using RestSrvr.Message;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Util;
using SanteDB.Rest.Common.Fault;
using SanteDB.Rest.Common.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;

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
            try
            {
#if DEBUG
                this.m_tracer.TraceWarning("Error on pipeline: {0}", error);

#else
                if (error is TargetInvocationException)
                    this.m_tracer.TraceWarning("{0} - {1} / {2}", RestOperationContext.Current.EndpointOperation.Description.InvokeMethod.Name, error.Message, error.InnerException?.Message);
                else
                    this.m_tracer.TraceWarning("{0} - {1}", RestOperationContext.Current.EndpointOperation.Description.InvokeMethod.Name, error.Message);
#endif

                if(faultMessage == null)
                {
                    if(RestOperationContext.Current.OutgoingResponse == null)
                    {
                        this.m_tracer.TraceWarning("Client hangup");
                        return false;
                    }
                    this.m_tracer.TraceWarning("For some reason the fault message is null - ");
                    faultMessage = new RestResponseMessage(RestOperationContext.Current.OutgoingResponse);
                }
                var ie = error;
                while (ie != null)
                {
                    this.m_tracer.TraceWarning("{0} - ({1}){2} - {3}", error == ie ? "" : "Caused By",
                        RestOperationContext.Current.EndpointOperation?.Description.InvokeMethod.Name,
                        ie?.GetType().FullName, ie.Message);

                    // TODO: Do we need this or can we just capture the innermost exception as the cause?
                    if (ie is RestClientException<RestServiceFault> || ie is SecurityException || ie is DetectedIssueException
                        || ie is FileNotFoundException || ie is KeyNotFoundException)
                        error = ie;
                    ie = ie.InnerException;
                }
                faultMessage.StatusCode = WebErrorUtility.ClassifyException(error);

                object fault = (error as RestClientException<RestServiceFault>)?.Result ?? new RestServiceFault(error);

                if (error is FaultException && error?.GetType() != typeof(FaultException)) // Special classification
                    fault = error?.GetType().GetRuntimeProperty("Body").GetValue(error);

                var formatter = RestMessageDispatchFormatter.CreateFormatter(RestOperationContext.Current.ServiceEndpoint.Description.Contract.Type);
                if (formatter != null)
                    formatter.SerializeResponse(faultMessage, null, fault);
                else
                    RestOperationContext.Current.OutgoingResponse.OutputStream.Write(System.Text.Encoding.UTF8.GetBytes(error.Message), 0, System.Text.Encoding.UTF8.GetByteCount(error.Message));

                try
                {
                    if (ApplicationServiceContext.Current.GetService<IOperatingSystemInfoService>()?.OperatingSystem != OperatingSystemID.Android)
                        AuditUtil.AuditNetworkRequestFailure(error, RestOperationContext.Current.IncomingRequest.Url, RestOperationContext.Current.IncomingRequest.Headers.AllKeys.ToDictionary(o => o, o => RestOperationContext.Current.IncomingRequest.Headers[o]), RestOperationContext.Current.OutgoingResponse.Headers.AllKeys.ToDictionary(o => o, o => RestOperationContext.Current.OutgoingResponse.Headers[o]));
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not send network request failure - {0}", e);
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error providing fault: {0}", e);
            }
            return true;
        }
    }
}