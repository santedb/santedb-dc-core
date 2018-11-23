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
using RestSrvr.Message;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Represents the default AGS Authorization behavior
    /// </summary>
    public class AgsAuthorizationServiceBehavior : IServiceBehavior, IServicePolicy
    {

        // Tracer for this class
        private Tracer m_tracer = Tracer.GetTracer(typeof(AgsAuthorizationServiceBehavior));

        /// <summary>
        /// Apply the specified policy
        /// </summary>
        public void Apply(RestRequestMessage request)
        {

            // Session cookie?
            if (request.Cookies["_s"] != null)
            {
                var cookie = request.Cookies["_s"];
                if (!cookie.Expired)
                {
                    var smgr = ApplicationContext.Current.GetService<ISessionManagerService>();
                    var session = smgr.Get(cookie.Value);
                    if (session != null)
                    {
                        try
                        {
                            AuthenticationContext.Current = AuthenticationContext.CurrentUIContext = new AuthenticationContext(session);
                            this.m_tracer.TraceVerbose("Retrieved session {0} from cookie", session?.Key);
                        }
                        catch (SessionExpiredException)
                        {
                            this.m_tracer.TraceWarning("Session {0} is expired and could not be extended", cookie.Value);
                            RestOperationContext.Current.OutgoingResponse.SetCookie(new Cookie("_s", Guid.Empty.ToString(), "/") { Expired = true, Expires = DateTime.Now.AddSeconds(-20) });
                        }
                    }
                    else // No session found
                    {
                        this.m_tracer.TraceWarning("Session {0} is not registered with the session provider", cookie.Value);
                        RestOperationContext.Current.OutgoingResponse.SetCookie(new Cookie("_s", Guid.Empty.ToString(), "/") { Expired = true, Expires = DateTime.Now.AddSeconds(-20) });
                    }
                }
            }

            // Authorization header
            if (request.Headers["Authorization"] != null)
            {
                var authHeader = request.Headers["Authorization"].Split(' ');
                switch (authHeader[0].ToLowerInvariant()) // Type / scheme
                {
                    case "basic":
                        {
                            var idp = ApplicationContext.Current.GetService<IIdentityProviderService>();
                            var authString = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader[1])).Split(':');
                            var principal = idp.Authenticate(authString[0], authString[1]);
                            if (principal == null)
                                throw new UnauthorizedAccessException();
                            else
                                AuthenticationContext.Current = AuthenticationContext.CurrentUIContext = new AuthenticationContext(principal);
                            this.m_tracer.TraceVerbose("Performed BASIC auth for {0}", AuthenticationContext.Current.Principal.Identity.Name);

                            break;
                        }
                    case "bearer":
                        {
                            var smgr = ApplicationContext.Current.GetService<ISessionManagerService>();
                            var session = smgr.Get(authHeader[1]);
                            if (session != null)
                            {
                                try
                                {
                                    AuthenticationContext.Current = AuthenticationContext.CurrentUIContext = new AuthenticationContext(session);
                                    this.m_tracer.TraceVerbose("Retrieved session {0} from cookie", session?.Key);
                                }
                                catch (SessionExpiredException)
                                {
                                    this.m_tracer.TraceWarning("Session {0} is expired and could not be extended", authHeader[1]);
                                    throw new UnauthorizedAccessException("Session is expired");
                                }
                            }
                            else // Something wrong??? Perhaps it is an issue with the thingy?
                                throw new UnauthorizedAccessException("Session is invalid");
                            break;
                        }

                }
            }
        }

        /// <summary>
        /// Apply the policy to the dispatcher
        /// </summary>
        public void ApplyServiceBehavior(RestService service, ServiceDispatcher dispatcher)
        {
            dispatcher.AddServiceDispatcherPolicy(this);
        }
    }
}
