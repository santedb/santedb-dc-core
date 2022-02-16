/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
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
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Jobs
{
    /// <summary>
    /// Mail synchronization job
    /// </summary>
    public class MailSynchronizationJob : IJob
    {

        /// <summary>
        /// Gets the unique identifier of this job
        /// </summary>
        public Guid Id => Guid.Parse("BE5F5126-AE97-4851-9741-2A329C5E7F79");

        // Cached credetials
        private IPrincipal m_cachedCredential = null;

        // Tracer for alerts
        private Tracer m_tracer = Tracer.GetTracer(typeof(MailSynchronizationJob));

        // Configuration
        private readonly SynchronizationConfigurationSection m_configuration;

        // Security configuration
        private readonly SecurityConfigurationSection m_securityConfiguration;

        // Alert repository
        private readonly IMailMessageRepositoryService m_mailRepository;
        private readonly IJobStateManagerService m_jobStateManager;
        private readonly ISynchronizationLogService m_synchronizationLogService;

        /// <summary>
        /// Creates a new job
        /// </summary>
        public MailSynchronizationJob(IConfigurationManager configurationManager, IMailMessageRepositoryService mailRepositoryService, IJobStateManagerService jobStateManagerService, ISynchronizationLogService synchronizationLogService)
        {
            this.m_configuration = configurationManager.GetSection<SynchronizationConfigurationSection>();
            this.m_securityConfiguration = configurationManager.GetSection<SecurityConfigurationSection>();
            this.m_mailRepository = mailRepositoryService;
            this.m_jobStateManager = jobStateManagerService;
            this.m_synchronizationLogService = synchronizationLogService;
        }

        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "Synchronize Mail Messages";

        /// <inheritdoc/>
        public string Description => "Synchronizes the dCDR messages (broadcasts, alerts, user messages) from central server infrastructure";

        /// <summary>
        /// Can cancel the job
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Parameters for the job
        /// </summary>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>();

        /// <summary>
        /// Gets current credentials
        /// </summary>
        private IDisposable GetCredentials(IRestClient client, out Credentials credentials)
        {
            var retVal = AuthenticationContextExtensions.EnterDeviceContext();
            credentials = client.Description.Binding.Security.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);
            return retVal;
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
                this.m_jobStateManager.SetState(this, JobStateType.Running);

                // We are to poll for alerts always (never push supported)
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                using (this.GetCredentials(amiClient.Client, out Credentials credentials))
                {
                    amiClient.Client.Credentials = credentials;

                    // When was the last time we polled an alert?
                    var lastSync = this.m_synchronizationLogService.GetLastTime(typeof(MailMessage));
                    var syncTime = new DateTimeOffset(lastSync.GetValueOrDefault());
                    // Poll action for all alerts to "everyone"
                    AmiCollection serverAlerts = amiClient.GetMailMessages(a => a.CreationTime >= syncTime && a.RcptTo.Any(o => o.UserName == "SYSTEM")); // SYSTEM WIDE ALERTS


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

                    this.m_synchronizationLogService.Save(typeof(MailMessage), String.Empty, String.Empty, "Mail", DateTime.Now);

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
                    this.m_jobStateManager.SetState(this, JobStateType.Completed);
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Could not pull alerts: {0}", ex.Message);
                this.m_jobStateManager.SetState(this, JobStateType.Aborted);
                this.m_jobStateManager.SetProgress(this, ex.Message, 0.0f);
            }

        }
    }
}
