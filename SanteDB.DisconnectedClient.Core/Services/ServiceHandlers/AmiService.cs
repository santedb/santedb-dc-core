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
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services.Attributes;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Model.Collection;
using SanteDB.DisconnectedClient.Services.Model;
using SanteDB.Core.Mail;
using SanteDB.Core.Diagnostics;

namespace SanteDB.DisconnectedClient.Services.ServiceHandlers
{
    /// <summary>
    /// Represents a service for the AMI
    /// </summary>
    [RestService("/__ami")]
    public class AmiService
    {

        // Get mail tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AmiService));

        /// <summary>
        /// Update the security user
        /// </summary>
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        [RestOperation(UriPath = "/SecurityUser", Method = "POST", FaultProvider = nameof(AmiFaultProvider))]
        public Object UpdateSecurityUser([RestMessage(RestMessageFormat.SimpleJson)] SecurityUserInfo user)
        {
            var localSecSrv = ApplicationContext.Current.GetService<IRepositoryService<SecurityUser>>();
            var amiServ = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

            if (user.PasswordOnly)
            {
                var idp = ApplicationContext.Current.GetService<IIdentityProviderService>();
                idp.ChangePassword(user.Entity.UserName.ToLower(), user.Entity.Password, AuthenticationContext.Current.Principal);
                return AuthenticationContext.Current.Session;
            }
            else
            {
                // Session
                amiServ.Client.Credentials = new TokenCredentials(AuthenticationContext.Current.Principal);
                var remoteUser = amiServ.GetUser(user.Entity.Key.Value);
                remoteUser.Entity.Email = user.Entity.Email;
                remoteUser.Entity.PhoneNumber = user.Entity.PhoneNumber;
                // Save the remote user in the local
                localSecSrv.Save(remoteUser.Entity);
                amiServ.UpdateUser(remoteUser.Entity.Key.Value, remoteUser);
                return remoteUser.Entity;
            }
        }

        /// <summary>
        /// Gets a user by username.
        /// </summary>
        /// <param name="username">The username of the user to be retrieved.</param>
        /// <returns>Returns the user.</returns>
        [RestOperation(Method = "GET", UriPath = "/SecurityUser")]
        [return: RestMessage(RestMessageFormat.Json)]
        public IdentifiedData GetUser()
        {
            // this is used for the forgot password functionality
            // need to find a way to stop people from simply searching users via username...

            NameValueCollection query = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
            var predicate = QueryExpressionParser.BuildLinqExpression<SecurityUser>(query);
            ISecurityRepositoryService securityRepositoryService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

            return null;
            //if (query.ContainsKey("_id"))
            //    return securityRepositoryService.GetUser(Guid.Parse(query["_id"][0]));
            //else
            //    return Bundle.CreateBundle(securityRepositoryService.FindUsers(predicate), 0, 0);
        }

        /// <summary>
        /// Care plan fault provider
        /// </summary>
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public ErrorResult AmiFaultProvider(Exception e)
        {
            return new ErrorResult() { Error = e.Message, ErrorDescription = e.InnerException?.Message, ErrorType = e.GetType().Name };
        }

        /// <summary>
        /// Get the alerts from the service
        /// </summary>
        [RestOperation(UriPath = "/mail", Method = "GET")]
        public List<MailMessage> GetMailMessages()
        {
            try
            {
                // Gets the specified alert messages
                NameValueCollection query = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);

                var alertService = ApplicationContext.Current.GetService<IMailMessageRepositoryService>();

                List<string> key = null;

                if (query.ContainsKey("id") && query.TryGetValue("id", out key))
                {
                    var id = key?.FirstOrDefault();

                    return new List<MailMessage> { alertService.Get(Guid.Parse(id)) };
                }

                var predicate = QueryExpressionParser.BuildLinqExpression<MailMessage>(query);
                int offset = query.ContainsKey("_offset") ? Int32.Parse(query["_offset"][0]) : 0,
                    count = query.ContainsKey("_count") ? Int32.Parse(query["_count"][0]) : 100;



                int totalCount = 0;

                return alertService.Find(predicate, offset, count, out totalCount).ToList();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Could not retrieve alerts {0}...", e);
                throw;
            }
        }

        /// <summary>
        /// Get the alerts from the service
        /// </summary>
        [RestOperation(UriPath = "/mail", Method = "POST")]
        public MailMessage SaveAlert([RestMessage(RestMessageFormat.SimpleJson)]MailMessage alert)
        {
            try
            {
                // Gets the specified alert messages
                var alertService = ApplicationContext.Current.GetService<IMailMessageRepositoryService>();
                alertService.Save(alert);
                return alert;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Could not retrieve alerts {0}...", e);
                return null;
            }
        }

    }
}
