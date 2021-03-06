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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Security;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.Subscribers
{
    /// <summary>
    /// Subscriber which performs policy enforcement
    /// </summary>
    public class PolicyEnforcementSubscriber : IDaemonService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Policy Enforcement Point Subscriber";

        // Running flag
        private bool m_isRunning = false;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(PolicyEnforcementSubscriber));
        /// <summary>
        /// Returns true when the daemon is running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return this.m_isRunning;
            }
        }

        /// <summary>
        /// Fired when the subscriber has started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when the subscriber is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when the subscriber is stopped
        /// </summary>
        public event EventHandler Stopped;
        /// <summary>
        /// Fired when the subscriber is stopping
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        /// Starts the subscriber
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            try
            {
                // Attach the subscriptions here
                foreach (var t in typeof(Entity).Assembly.GetTypes().Where(t => typeof(Entity).IsAssignableFrom(t) || typeof(Act).IsAssignableFrom(t)))
                {
                    var idpType = typeof(IDataPersistenceService<>).MakeGenericType(new Type[] { t });
                    var idpInstance = ApplicationContext.Current.GetService(idpType);
                    var mi = typeof(PolicyEnforcementSubscriber).GetMethod(nameof(BindClinicalEnforcement)).MakeGenericMethod(new Type[] { t });
                    mi.Invoke(this, new object[] { idpInstance });
                }

                this.Started?.Invoke(this, EventArgs.Empty);
                this.m_isRunning = true;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error starting policy enformcent point: {0}", e);
            }
            return this.m_isRunning;
        }

        /// <summary>
        /// Bind the enforcement point
        /// </summary>
        protected void BindClinicalEnforcement<TData>(IDataPersistenceService<TData> persister) where TData : IdentifiedData
        {

            // Demand query
            persister.Querying += (o, e) =>
            {
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.QueryClinicalData).Demand();
            };

            // Demand insert
            persister.Inserting += (o, e) =>
            {
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.WriteClinicalData).Demand();
            };

            // Demand update
            persister.Updating += (o, e) =>
            {
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.WriteClinicalData).Demand();
            };

            // Obsoletion permission demand
            persister.Obsoleting += (o, e) =>
            {
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.DeleteClinicalData).Demand();
            };

            // Queried data filter
            persister.Queried += (o, e) =>
            {
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.ReadClinicalData).Demand();
                QueryResultEventArgs<TData> dqre = e as QueryResultEventArgs<TData>;
                // Filter dataset
                if (dqre != null)
                {
                    dqre.Results = dqre.Results.Where(i => ApplicationContext.Current.PolicyDecisionService.GetPolicyDecision(AuthenticationContext.Current.Principal, i).Outcome == SanteDB.Core.Model.Security.PolicyGrantType.Grant);
                }
            };
        }

        /// <summary>
        /// Stop the execution of the enforcement daemon
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