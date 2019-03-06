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
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Ags.Util;
using SanteDB.DisconnectedClient.Core;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Represents a web error handler that is intended for intercepting web errors
    /// </summary>
    public class AgsWebErrorHandlerServiceBehavior : IServiceBehavior, IServiceErrorHandler
    {
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
