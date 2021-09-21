﻿/*
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
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Exceptions;
using System;
using System.Linq;
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
                            {
                                var contextAuth = AuthenticationContext.EnterContext(principal);
                                RestOperationContext.Current.Disposed += (o, e) => contextAuth.Dispose();
                            }
                            this.m_tracer.TraceVerbose("Performed BASIC auth for {0}", AuthenticationContext.Current.Principal.Identity.Name);

                            break;
                        }
                    case "bearer":
                        {
                            var contextAuth = this.SetContextFromBearer(authHeader[1]);
                            RestOperationContext.Current.Disposed += (o, e) => contextAuth.Dispose();
                            break;
                        }

                }
            }
            else if (request.Url.Query.Contains("_sessionId="))
            {
                var query = NameValueCollection.ParseQueryString(request.Url.Query);
                var session = query["_sessionId"][0];
                this.SetContextFromBearer(session);
            }
            else if (request.Cookies["_s"] != null) // cookie authentication
            {
                var token = request.Cookies["_s"].Value;
                this.SetContextFromBearer(token);
            }

        }

        /// <summary>
        /// Session token
        /// </summary>
        private IDisposable SetContextFromBearer(string bearerToken)
        {
            var smgr = ApplicationContext.Current.GetService<ISessionProviderService>();

            var bearerBinary = bearerToken.ParseHexString();
            var sessionId = bearerBinary.Take(16).ToArray();
            var signature = bearerBinary.Skip(16).ToArray();

            if (!ApplicationServiceContext.Current.GetService<IDataSigningService>().Verify(sessionId, signature))
                throw new SecurityTokenException(SecurityTokenExceptionType.InvalidSignature, "Token has been tampered");

            // Get the session
            var session = ApplicationServiceContext.Current.GetService<ISessionProviderService>().Get(
                sessionId
            );

            if (session == null)
            {
                return AuthenticationContext.EnterContext(AuthenticationContext.AnonymousPrincipal);
            }

            IPrincipal principal = ApplicationServiceContext.Current.GetService<ISessionIdentityProviderService>().Authenticate(session);
            if (principal == null)
                throw new SecurityTokenException(SecurityTokenExceptionType.KeyNotFound, "Invalid bearer token");

            RestOperationContext.Current.Data.Add(SessionPropertyName, session);

            this.m_tracer.TraceInfo("User {0} authenticated via SESSION BEARER", principal.Identity.Name);
            return AuthenticationContext.EnterContext(principal);
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
