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
using RestSrvr.Exceptions;
using RestSrvr.Message;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SharpCompress.Compressors.Deflate;
using System;
using System.IO;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Checks request against magic generated 
    /// </summary>
    public class AgsMagicServiceBehavior : IServiceBehavior, IServicePolicy
    {

        /// <summary>
        /// Tracer 
        /// </summary>
        private Tracer m_tracer = Tracer.GetTracer(typeof(AgsMagicServiceBehavior));

        /// <summary>
        /// Apply the service policy
        /// </summary>
        public void Apply(RestRequestMessage request)
        {
            if (request.Headers["X-OIZMagic"] == ApplicationContext.Current.ExecutionUuid.ToString() ||
                request.UserAgent == $"SanteDB-DC {ApplicationContext.Current.ExecutionUuid}" ||
                ApplicationContext.Current.ExecutionUuid.ToString() == ApplicationContext.Current.ConfigurationManager.GetAppSetting("http.bypassMagic"))
                ;
            else
            {
                // Something wierd with the appp, show them the nice message
                if (request.UserAgent.StartsWith("SanteDB"))
                    throw new FaultException<String>(403, "Hmm, something went wrong. For security's sake we can't show the information you requested. Perhaps restarting the application will help");
                else // User is using a browser to try and access this? How dare they
                {
                    RestOperationContext.Current.OutgoingResponse.ContentType = "text/html";
                    throw new FaultException<Stream>(403, new GZipStream(typeof(AgsMagicServiceBehavior).Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.Ags.Resources.antihaxor"), SharpCompress.Compressors.CompressionMode.Decompress));
                }
            }
        }

        /// <summary>
        /// Add this service behavior
        /// </summary>
        public void ApplyServiceBehavior(RestService service, ServiceDispatcher dispatcher)
        {
            dispatcher.AddServiceDispatcherPolicy(this);
        }
    }
}
