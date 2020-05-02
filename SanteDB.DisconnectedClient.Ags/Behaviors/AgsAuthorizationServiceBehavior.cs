/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using RestSrvr;
using RestSrvr.Message;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Exceptions;
using System;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Represents the default AGS Authorization behavior
    /// </summary>
    public class AgsAuthorizationServiceBehavior : IServiceBehavior, IServicePolicy
    {

        // Tracer for this class
        private Tracer m_tracer = Tracer.GetTracer(typeof(AgsAuthorizationServiceBehavior));

        // Session property name
        public const string SessionPropertyName = "Session";

        /// <summary>
        /// Apply the specified policy
        /// </summary>
        public void Apply(RestRequestMessage request)
        {
            try
            {

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
                                    AuthenticationContext.Current = new AuthenticationContext(principal);
                                this.m_tracer.TraceVerbose("Performed BASIC auth for {0}", AuthenticationContext.Current.Principal.Identity.Name);

                                break;
                            }
                        case "bearer":
                            {
                                this.SetContextFromBearer(authHeader[1]);
                                break;
                            }

                    }
                }
                else if(request.Url.Query.Contains("_sessionId="))
                {
                    var query = NameValueCollection.ParseQueryString(request.Url.Query);
                    var session = query["_sessionId"][0];
                    this.SetContextFromBearer(session);
                }
            }
            finally
            {
                RestOperationContext.Current.Disposed += (o, e) => AuthenticationContext.Current = new AuthenticationContext(AuthenticationContext.AnonymousPrincipal);
            }
        }

        /// <summary>
        /// Session token
        /// </summary>
        /// <param name="sessionToken"></param>
        private void SetContextFromBearer(string sessionToken)
        {
            var smgr = ApplicationContext.Current.GetService<ISessionProviderService>();

            // Get the session
            var session = ApplicationServiceContext.Current.GetService<ISessionProviderService>().Get(
                Enumerable.Range(0, sessionToken.Length)
                                    .Where(x => x % 2 == 0)
                                    .Select(x => Convert.ToByte(sessionToken.Substring(x, 2), 16))
                                    .ToArray()
            );
            if (session == null)
                throw new SecurityTokenException(SecurityTokenExceptionType.KeyNotFound, "Bearer token not valid");

            IPrincipal principal = ApplicationServiceContext.Current.GetService<ISessionIdentityProviderService>().Authenticate(session);
            if (principal == null)
                throw new SecurityTokenException(SecurityTokenExceptionType.KeyNotFound, "Invalid bearer token");

            RestOperationContext.Current.Data.Add(SessionPropertyName, session);
            AuthenticationContext.Current = new AuthenticationContext(principal);

            this.m_tracer.TraceInfo("User {0} authenticated via SESSION BEARER", principal.Identity.Name);
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
