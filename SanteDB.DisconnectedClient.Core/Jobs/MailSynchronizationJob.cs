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
using SanteDB.Core.Api.Security;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Jobs;
using SanteDB.Core.Mail;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Jobs
{
    /// <summary>
    /// Mail synchronization job
    /// </summary>
    public class MailSynchronizationJob : IJob
    {

        // Cached credetials
        private IPrincipal m_cachedCredential = null;

        // Tracer for alerts
        private Tracer m_tracer = Tracer.GetTracer(typeof(MailSynchronizationJob));

        // Configuration
        private SynchronizationConfigurationSection m_configuration;

        // Security configuration
        private SecurityConfigurationSection m_securityConfiguration;

        // Alert repository
        private IMailMessageRepositoryService m_mailRepository;

        /// <summary>
        /// Creates a new job
        /// </summary>
        public MailSynchronizationJob()
        {
            this.m_configuration = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>();
            this.m_securityConfiguration = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();
            this.m_mailRepository = ApplicationContext.Current.GetService<IMailMessageRepositoryService>();

        }

        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "Synchronize Mail Messages";

        /// <summary>
        /// Can cancel the job
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Current state of the job
        /// </summary>
        public JobStateType CurrentState { get; private set; }

        /// <summary>
        /// Parameters for the job
        /// </summary>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>();

        /// <summary>
        /// Last time the job was started
        /// </summary>
        public DateTime? LastStarted { 
            get; 
            private set; 
        }

        /// <summary>
        /// Last time the job was finished
        /// </summary>
        public DateTime? LastFinished
        {
            get
            {
                try
                {
                    var logSvc = ApplicationContext.Current.GetService<ISynchronizationLogService>();
                    return logSvc.GetLastTime(typeof(MailMessage));
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error getting last finished date: {0}", e);
                    return null;
                }
            }
            set
            {
                try
                {
                    var logSvc = ApplicationContext.Current.GetService<ISynchronizationLogService>();
                    logSvc.Save(typeof(MailMessage), null, null, "mail", value.Value);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error getting last finished date: {0}", e);
                }
            }
        }

        /// <summary>
        /// Gets current credentials
        /// </summary>
        private Credentials GetCredentials(IRestClient client)
        {
            var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            AuthenticationContext.Current = new AuthenticationContext(this.m_cachedCredential ?? AuthenticationContext.Current.Principal);

            // TODO: Clean this up - Login as device account
            if (!AuthenticationContext.Current.Principal.Identity.IsAuthenticated ||
                ((AuthenticationContext.Current.Principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTimeOffset.MinValue) < DateTimeOffset.Now)
            {
                AuthenticationContext.Current = new AuthenticationContext(ApplicationContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret));
                this.m_cachedCredential = AuthenticationContext.Current.Principal;
            }
            return client.Description.Binding.Security.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);

        }

        /// <summary>
        /// Cancel the job
        /// </summary>
        public void Cancel()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Run the mail synchronization service
        /// </summary>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {

                this.LastStarted = DateTime.Now;
                this.CurrentState = JobStateType.Running;

                // We are to poll for alerts always (never push supported)
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);
                
                // When was the last time we polled an alert?
                var syncTime = this.LastFinished.HasValue ? new DateTimeOffset(this.LastFinished.Value) : new DateTimeOffset(new DateTime(1900, 01, 01));
                // Poll action for all alerts to "everyone"
                AmiCollection serverAlerts = amiClient.GetMailMessages(a => a.CreationTime >= syncTime && a.RcptTo.Any(o=>o.UserName == "SYSTEM")); // SYSTEM WIDE ALERTS


                // TODO: We need to filter by users in which this tablet will be interested in
                ParameterExpression userParameter = Expression.Parameter(typeof(SecurityUser), "u");
                // User name filter
                Expression userNameFilter = Expression.Equal(Expression.MakeMemberAccess(userParameter, userParameter.Type.GetRuntimeProperty("UserName")), Expression.Constant(this.m_securityConfiguration.DeviceName));

                // Or eith other users which have logged into this tablet
                foreach (var user in ApplicationContext.Current.GetService<IDataPersistenceService<SecurityUser>>().Query(u => u.LastLoginTime != null && u.UserName != this.m_securityConfiguration.DeviceName, AuthenticationContext.SystemPrincipal))
                    userNameFilter = Expression.OrElse(userNameFilter,
                        Expression.Equal(Expression.MakeMemberAccess(userParameter, userParameter.Type.GetRuntimeProperty("UserName")), Expression.Constant(user.UserName))
                        );

                ParameterExpression parmExpr = Expression.Parameter(typeof(MailMessage), "a");
                Expression timeExpression = Expression.GreaterThanOrEqual(
                    Expression.Convert(Expression.MakeMemberAccess(parmExpr, parmExpr.Type.GetRuntimeProperty("CreationTime")), typeof(DateTimeOffset)),
                    Expression.Constant(syncTime)
                ),
                // this tablet expression
                userExpression = Expression.Call(
                    (MethodInfo)typeof(Enumerable).GetGenericMethod("Any", new Type[] { typeof(SecurityUser) }, new Type[] { typeof(IEnumerable<SecurityUser>), typeof(Func<SecurityUser, bool>) }),
                    Expression.MakeMemberAccess(parmExpr, parmExpr.Type.GetRuntimeProperty("RcptTo")),
                    Expression.Lambda<Func<SecurityUser, bool>>(userNameFilter, userParameter));

                serverAlerts.CollectionItem = serverAlerts.CollectionItem.Union(amiClient.GetMailMessages(Expression.Lambda<Func<MailMessage, bool>>(Expression.AndAlso(timeExpression, userExpression), parmExpr)).CollectionItem).ToList();

                // Import the alerts
                foreach (var itm in serverAlerts.CollectionItem.OfType<MailMessage>())
                {
                    this.m_tracer.TraceVerbose("Importing ALERT: [{0}]: {1}", itm.TimeStamp, itm.Subject);
                    itm.Body = String.Format("<pre>{0}</pre>", itm.Body);
                    this.m_mailRepository.Broadcast(itm);
                }

                // Push alerts which I have created or updated
                //int tc = 0;
                //foreach(var itm in this.m_alertRepository.Find(a=> (a.TimeStamp >= lastTime ) && a.Flags != AlertMessageFlags.System, 0, null, out tc))
                //{
                //    if (!String.IsNullOrEmpty(itm.To))
                //    {
                //        this.m_tracer.TraceVerbose("Sending ALERT: [{0}]: {1}", itm.TimeStamp, itm.Subject);
                //        if (itm.UpdatedTime != null)
                //            amiClient.UpdateAlert(itm.Key.ToString(), new AlertMessageInfo(itm));
                //        else
                //            amiClient.CreateAlert(new AlertMessageInfo(itm));
                //    }
                //}

                this.LastFinished = DateTime.Now;
                this.CurrentState = JobStateType.Completed;
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Could not pull alerts: {0}", ex.Message);
                this.CurrentState = JobStateType.Cancelled;
            }

        }
    }
}
