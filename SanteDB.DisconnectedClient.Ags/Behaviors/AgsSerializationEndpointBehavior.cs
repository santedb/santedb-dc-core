using RestSrvr;
using SanteDB.DisconnectedClient.Ags.Formatter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Serialization for the endpoint
    /// </summary>
    public class AgsSerializationEndpointBehavior : IEndpointBehavior, IOperationBehavior
    {
        /// <summary>
        /// Apply the behavior
        /// </summary>
        public void ApplyEndpointBehavior(ServiceEndpoint endpoint, EndpointDispatcher dispatcher)
        {
            foreach (var op in endpoint.Description.Contract.Operations)
                op.AddOperationBehavior(this);
        }

        /// <summary>
        /// Apply operation behavior
        /// </summary>
        public void ApplyOperationBehavior(EndpointOperation operation, OperationDispatcher dispatcher)
        {
            dispatcher.DispatchFormatter = AgsMessageDispatchFormatter.CreateFormatter(operation.Description.Contract.Type);
        }
    }
}
