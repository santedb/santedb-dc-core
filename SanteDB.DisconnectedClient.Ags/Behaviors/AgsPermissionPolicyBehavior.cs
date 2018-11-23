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
 * Date: 2018-11-23
 */
using System.Reflection;
using RestSrvr;
using RestSrvr.Message;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SanteDB.DisconnectedClient.Xamarin.Security;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Represents a policy permission behavior for validating the current context has authorization to perform an operation
    /// </summary>
    public class AgsPermissionPolicyBehavior : IServiceBehavior, IEndpointBehavior, IOperationBehavior, IOperationPolicy
    {
        /// <summary>
        /// Apply the actual policy
        /// </summary>
        public void Apply(EndpointOperation operation, RestRequestMessage request)
        {
            foreach (var ppe in operation.Description.InvokeMethod.GetCustomAttributes<DemandAttribute>())
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
