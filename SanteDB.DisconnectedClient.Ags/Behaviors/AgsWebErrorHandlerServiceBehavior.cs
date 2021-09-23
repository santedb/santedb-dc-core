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
using RestSrvr.Message;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security.Audit;
using SanteDB.DisconnectedClient.Ags.Util;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Represents a web error handler that is intended for intercepting web errors
    /// </summary>
    public class AgsWebErrorHandlerServiceBehavior : IServiceBehavior, IServiceErrorHandler
    {
        private Tracer m_tracer = Tracer.GetTracer(typeof(AgsWebErrorHandlerServiceBehavior));

        /// <summary>
        /// Apply the service behavior
        /// </summary>
        public void ApplyServiceBehavior(RestService service, ServiceDispatcher dispatcher)
        {
            dispatcher.ErrorHandlers.Clear();
            dispatcher.ErrorHandlers.Add(this);
        }

        /// <summary>
        /// True if this service can handle the error
        /// </summary>
        public bool HandleError(Exception error)
        {
            return true;
        }

        /// <summary>
        /// Provide the fault
        /// </summary>
        public bool ProvideFault(Exception error, RestResponseMessage response)
        {
            var errCode = WebErrorUtility.ClassifyException(error, true);
            var hdlr = ApplicationContext.Current.GetService<IAppletManagerService>().Applets.SelectMany(o => o.ErrorAssets).FirstOrDefault(o => o.ErrorCode == errCode);

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

            // Grab the asset handler
            try
            {
                if (hdlr != null)
                {
                    response.Body = new MemoryStream(new byte[0]);
                    RestOperationContext.Current.OutgoingResponse.Redirect(hdlr.Asset);
                }
                else
                {
                    RestOperationContext.Current.OutgoingResponse.StatusCode = errCode;
                    using (var sr = new StreamReader(typeof(AgsWebErrorHandlerServiceBehavior).Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.Ags.Resources.GenericError.html")))
                    {
                        string errRsp = sr.ReadToEnd().Replace("{status}", response.StatusCode.ToString())
                            .Replace("{description}", response.StatusDescription)
                            .Replace("{type}", error.GetType().Name)
                            .Replace("{message}", error.Message)
                            .Replace("{details}", error.ToString())
                            .Replace("{trace}", error.StackTrace);
                        RestOperationContext.Current.OutgoingResponse.ContentType = "text/html";
                        response.Body = new MemoryStream(Encoding.UTF8.GetBytes(errRsp));
                    }
                }

                AuditUtil.AuditNetworkRequestFailure(error, RestOperationContext.Current.IncomingRequest.Url, RestOperationContext.Current.IncomingRequest.Headers.AllKeys.ToDictionary(o => o, o => RestOperationContext.Current.IncomingRequest.Headers[o]), RestOperationContext.Current.OutgoingResponse.Headers.AllKeys.ToDictionary(o => o, o => RestOperationContext.Current.OutgoingResponse.Headers[o]));

                return true;
            }
            catch (Exception e)
            {
                Tracer.GetTracer(typeof(AgsWebErrorHandlerServiceBehavior)).TraceError("Could not provide fault: {0}", e.ToString());
                throw;
            }
        }
    }
}