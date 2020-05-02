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
using SanteDB.DisconnectedClient.Security;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Represents a policy permission behavior for validating the current context has authorization to perform an operation
    /// </summary>
    public class AgsPermissionPolicyBehavior : IServiceBehavior, IEndpointBehavior, IOperationBehavior, IOperationPolicy
    {

        // Behavior type
        private Type m_behaviorType = null;

        /// <summary>
        /// Creates a new demand policy
        /// </summary>
        public AgsPermissionPolicyBehavior(Type behaviorType)
        {
            this.m_behaviorType = behaviorType;
        }

        /// <summary>
        /// Apply the actual policy
        /// </summary>
        public void Apply(EndpointOperation operation, RestRequestMessage request)
        {
            var methInfo = this.m_behaviorType.GetMethod(operation.Description.InvokeMethod.Name, operation.Description.InvokeMethod.GetParameters().Select(p => p.ParameterType).ToArray());
            foreach (var ppe in methInfo.GetCustomAttributes<DemandAttribute>())
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, ppe.PolicyId).Demand();
        }

        /// <summary>
        /// Apply the endpoint behavior
        /// </summary>
        public void ApplyEndpointBehavior(ServiceEndpoint endpoint, EndpointDispatcher dispatcher)
        {
            foreach (var op in endpoint.Description.Contract.Operations)
                op.AddOperationBehavior(this);
        }

        /// <summary>
        /// Apply the operation policy behavior
        /// </summary>
        public void ApplyOperationBehavior(EndpointOperation operation, OperationDispatcher dispatcher)
        {
            dispatcher.AddOperationPolicy(this);
        }

        /// <summary>
        /// Apply the service behavior
        /// </summary>
        public void ApplyServiceBehavior(RestService service, ServiceDispatcher dispatcher)
        {
            foreach (var itm in service.Endpoints)
                itm.AddEndpointBehavior(this);
        }
    }
}
