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
using SanteDB.DisconnectedClient.Ags.Formatter;
using SanteDB.DisconnectedClient.Ags.Util;
using SanteDB.Rest.Common.Fault;
using System;
using System.Reflection;

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

            faultMessage.StatusCode = WebErrorUtility.ClassifyException(error);

            object fault = new RestServiceFault(error);

            if (error is FaultException && error.GetType() != typeof(FaultException)) // Special classification
                fault = error.GetType().GetRuntimeProperty("Body").GetValue(error);

            AgsMessageDispatchFormatter.CreateFormatter(RestOperationContext.Current.ServiceEndpoint.Description.Contract.Type).SerializeResponse(faultMessage, null, fault);
            return true;
        }
    }
}
