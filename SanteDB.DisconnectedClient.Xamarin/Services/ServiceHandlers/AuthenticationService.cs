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
 * User: fyfej
 * Date: 2017-9-1
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SanteDB.DisconnectedClient.Xamarin.Services.Attributes;
using SanteDB.DisconnectedClient.Xamarin.Security;
using System.Security;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.Core.Model.Security;
using System.Globalization;
using SanteDB.DisconnectedClient.Core.Security;
using System.Net;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.DisconnectedClient.Core.Diagnostics;
using SanteDB.Messaging.AMI.Client;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.i18n;

namespace SanteDB.DisconnectedClient.Xamarin.Services.ServiceHandlers
{
    /// <summary>
    /// Represents a service which handles authentication requests
    /// </summary>
    [RestService("/__auth")]
    public class AuthenticationService
    {

        // Get tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AuthenticationService));

        /// <summary>
        /// Abandons the users session.
        /// </summary>
        /// <returns>Returns an empty session.</returns>
        [RestOperation(Method = "DELETE", UriPath = "/session", FaultProvider = nameof(AuthenticationFault))]
        public SessionInfo Abandon()
        {
            var cookie = MiniHdsiServer.CurrentContext.Request.Cookies["_s"];

            var value = Guid.Empty;

            if (cookie != null && Guid.TryParse(cookie.Value, out value))
            {
                ISessionManagerService sessionService = ApplicationContext.Current.GetService<ISessionManagerService>();
                var sessionInfo = sessionService.Delete(value);
                if (MiniHdsiServer.CurrentContext.Request.Cookies["_s"] == null)
                    MiniHdsiServer.CurrentContext.Response.SetCookie(new Cookie("_s", Guid.Empty.ToString(), "/") { Expired = true, Expires = DateTime.Now.AddSeconds(-20) });

                if (sessionInfo != null)
                {
                    AuditUtil.AuditLogout(sessionInfo.Principal);
                }
            }

            return new SessionInfo();
        }

        /// <summary>
        /// Gets the TFA authentication mechanisms
        /// </summary>
        [RestOperation(Method = "POST", UriPath = "/tfa", FaultProvider = nameof(AuthenticationFault))]
        [return: RestMessage(RestMessageFormat.Json)]
        public bool SendTfaSecret([RestMessage(RestMessageFormat.Json)]TfaRequestInfo resetInfo)
        {
            try
            {
                var resetService = ApplicationContext.Current.GetService<ITwoFactorRequestService>();
                if (resetService == null)
                    throw new InvalidOperationException(Strings.err_reset_not_supported);
                resetService.SendVerificationCode(resetInfo.ResetMechanism, resetInfo.Verification, resetInfo.UserName, resetInfo.Purpose);
                return true;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting sending secret: {0}", e);
                throw;
            }


        }
        /// <summary>
        /// Gets the TFA authentication mechanisms
        /// </summary>
        [RestOperation(Method = "GET", UriPath = "/tfa", FaultProvider = nameof(AuthenticationFault))]
        [return: RestMessage(RestMessageFormat.Json)]
        public List<TfaMechanismInfo> GetTfaMechanisms()
        {
            try
            {
                var resetService = ApplicationContext.Current.GetService<ITwoFactorRequestService>();
                if (resetService == null)
                    throw new InvalidOperationException(Strings.err_reset_not_supported);
                return resetService.GetResetMechanisms();

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting TFA mechanisms: {0}", e.Message);
                throw;
            }

        }

        /// <summary>
        /// Authenticate the user returning the session if successful
        /// </summary>
        /// <param name="authRequest"></param>
        /// <returns></returns>
        [RestOperation(Method = "POST", UriPath = "/authenticate", FaultProvider = nameof(AuthenticationFault))]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public SessionInfo Authenticate([RestMessage(RestMessageFormat.FormData)] NameValueCollection authRequest)
        {

            ISessionManagerService sessionService = ApplicationContext.Current.GetService<ISessionManagerService>();
            SessionInfo retVal = null;

            List<String> usernameColl = null,
                tfaSecretColl = null,
                passwordColl = null,
                purposeOfUse = null;
            authRequest.TryGetValue("username", out usernameColl);
            authRequest.TryGetValue("password", out passwordColl);
            authRequest.TryGetValue("tfaSecret", out tfaSecretColl);
            authRequest.TryGetValue("purposeOfUse", out purposeOfUse);

            String username = usernameColl?.FirstOrDefault().ToLower(),
                password = passwordColl?.FirstOrDefault(),
                tfaSecret = tfaSecretColl?.FirstOrDefault();

            switch (authRequest["grant_type"][0])
            {
                case "password":
                    retVal = sessionService.Authenticate(username, password);
                    break;
                case "refresh":
                    retVal = sessionService.Refresh(AuthenticationContext.Current.Session, null); // Force a re-issue
                    break;
                case "tfa":
                    retVal = sessionService.Authenticate(username, password, tfaSecret);
                    break;
            }

            if (retVal == null)
            {
                throw new SecurityException();
            }
            else
            {
                var lanugageCode = retVal?.UserEntity?.LanguageCommunication?.FirstOrDefault(o => o.IsPreferred)?.LanguageCode;

                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(CultureInfo.DefaultThreadCurrentUICulture?.TwoLetterISOLanguageName ?? "en");

                if (lanugageCode != null)
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lanugageCode);

                // Set the session 
                if (authRequest.ContainsKey("scope") && authRequest["scope"][0] == "*") // Requesting all access so we need to send back a session ID :)
                    MiniHdsiServer.CurrentContext.Response.SetCookie(new Cookie("_s", retVal.Token)
                    {
                        HttpOnly = true,
                        Secure = true,
                        Path = "/",
                        Domain = MiniHdsiServer.CurrentContext.Request.Url.Host
                    });
                return retVal;
            }
        }

        /// <summary>
        /// Authentication fault
        /// </summary>
        public OAuthTokenResponse AuthenticationFault(Exception e)
        {
            if (e.Data.Contains("detail"))
                return e.Data["detail"] as OAuthTokenResponse;
            else
                return new OAuthTokenResponse()
                {
                    Error = e.Message,
                    ErrorDescription = e.InnerException?.Message
                };
        }


        /// <summary>
        /// Authenticate the user returning the session if successful
        /// </summary>
        /// <param name="authRequest"></param>
        /// <returns></returns>
        [RestOperation(Method = "GET", UriPath = "/session")]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public SessionInfo GetSession()
        {
            NameValueCollection query = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
            ISessionManagerService sessionService = ApplicationContext.Current.GetService<ISessionManagerService>();

            if (query.ContainsKey("_id"))
                return sessionService.Get(Guid.Parse(query["_id"][0]));
            else
                return AuthenticationContext.Current.Session;
        }


    }
}
