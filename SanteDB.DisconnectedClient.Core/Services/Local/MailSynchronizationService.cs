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
using SanteDB.Core.Api.Security;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Mail;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Synchronization;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Represents an alert synchronization service
    /// </summary>
    public class MailSynchronizationService : IDaemonService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Mail Synchronization Service";

        // Cached credetials
        private IPrincipal m_cachedCredential = null;

        // Tracer for alerts
        private Tracer m_tracer = Tracer.GetTracer(typeof(MailSynchronizationService));

        // Running service
        private bool m_isRunning = false;

        // Configuration
        private SynchronizationConfigurationSection m_configuration;

        // Security configuration
        private SecurityConfigurationSection m_securityConfiguration;

        // Alert repository
        private IMailMessageRepositoryService m_mailRepository;

        /// <summary>
        /// True when the service is running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return this.m_isRunning;
            }
        }

        public event EventHandler Started;
        public event EventHandler Starting;
        public event EventHandler Stopped;
        public event EventHandler Stopping;

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
        /// Start the daemon service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_configuration = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>();
            this.m_securityConfiguration = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            // Application context has started
            ApplicationContext.Current.Started += (o, e) =>
            {
                try
                {
                    var logSvc = ApplicationContext.Current.GetService<ISynchronizationLogService>();

                    // We are to poll for alerts always (never push supported)
                    TimeSpan pollInterval = this.m_configuration.PollInterval == TimeSpan.MinValue ? new TimeSpan(0, 10, 0) : this.m_configuration.PollInterval;
                    this.m_mailRepository = ApplicationContext.Current.GetService<IMailMessageRepositoryService>();
                    Action<Object> pollAction = null;
                    pollAction = x =>
                    {
                        try
                        {
                            var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                            amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);
                            // Pull from alerts
                            if (!this.m_isRunning) return;

                            // When was the last time we polled an alert?
                            var lastTime = logSvc.GetLastTime(typeof(MailMessage));

                            var syncTime = lastTime.HasValue ? new DateTimeOffset(lastTime.Value) : DateTimeOffset.Now.AddHours(-1);

                            // Poll action for all alerts to "everyone"
                            AmiCollection serverAlerts = amiClient.GetMailMessages(a => a.CreationTime >= lastTime && a.To.Contains("everyone"));


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

                            logSvc.Save(typeof(MailMessage), null, null, null);
                        }
                        catch (Exception ex)
                        {
                            this.m_tracer.TraceError("Could not pull alerts: {0}", ex.Message);
                        }
                        finally
                        {
                            // Re-schedule myself in the poll interval time
                            ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(pollInterval, pollAction, null);
                        }
                    };

                    //ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(pollInterval, pollAction, null);
                    this.m_isRunning = true;

                    pollAction(null);
                }
                catch (Exception ex)
                {
                    this.m_tracer.TraceError("Error starting Alert Sync: {0}", ex.Message);
                }
                //this.m_alertRepository.Committed += 
            };

            this.Started?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.m_isRunning = false;

            this.Stopped?.Invoke(this, EventArgs.Empty);

            return true;
        }
    }
}
