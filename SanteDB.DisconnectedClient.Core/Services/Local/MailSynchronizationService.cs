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
using SanteDB.Core;
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
using SanteDB.DisconnectedClient.Jobs;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Services.Local
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

        /// <summary>
        /// True when the service is running
        /// </summary>
        public bool IsRunning { get; private set; }

        public event EventHandler Started;
        public event EventHandler Starting;
        public event EventHandler Stopped;
        public event EventHandler Stopping;

       
        /// <summary>
        /// Start the daemon service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            ApplicationServiceContext.Current.Started += (o, e) =>
            {
                var config = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>();
                
                ApplicationServiceContext.Current.GetService<IJobManagerService>().AddJob(new MailSynchronizationJob(), config.PollInterval);
                this.IsRunning = true;
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

            this.IsRunning = false;

            this.Stopped?.Invoke(this, EventArgs.Empty);

            return true;
        }
    }
}
