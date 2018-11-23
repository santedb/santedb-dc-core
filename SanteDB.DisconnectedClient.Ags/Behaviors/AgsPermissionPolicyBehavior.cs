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
