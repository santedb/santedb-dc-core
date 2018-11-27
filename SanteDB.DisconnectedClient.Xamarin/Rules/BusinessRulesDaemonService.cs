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
 * Date: 2018-6-28
 */
using SanteDB.BusinessRules.JavaScript;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Services;
using System;

namespace SanteDB.DisconnectedClient.Xamarin.Rules
{
    /// <summary>
    /// Business rules service which adds javascript files
    /// </summary>
    public class BusinessRulesDaemonService : IDaemonService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Business Rules Daemon Service";

        /// <summary>
        /// Indicates whether the service is running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return true;
            }
        }

        public event EventHandler Started;
        public event EventHandler Starting;
        public event EventHandler Stopped;
        public event EventHandler Stopping;

        /// <summary>
        /// Start the service which will register items with the business rules handler
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);
            ApplicationContext.Current.Started += (o, e) =>
            {
                try
                {
                    ApplicationServiceContext.Current = ApplicationContext.Current;
                    ApplicationServiceContext.HostType = SanteDBHostType.OtherClient;

                    if (ApplicationContext.Current.GetService<IDataReferenceResolver>() == null)
                        ApplicationContext.Current.AddServiceProvider(typeof(AppletDataReferenceResolver));
                    new AppletBusinessRuleLoader().LoadRules();

                }
                catch (Exception ex)
                {
                    Tracer.GetTracer(typeof(BusinessRulesDaemonService)).TraceError("Error starting up business rules service: {0}", ex);
                }
            };
            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Stopping
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
