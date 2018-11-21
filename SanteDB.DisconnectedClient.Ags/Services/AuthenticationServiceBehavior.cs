using RestSrvr.Attributes;
using SanteDB.DisconnectedClient.Ags.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// Authentication service behavior
    /// </summary>
    [ServiceBehavior(Name = "AUTH", InstanceMode = ServiceInstanceMode.PerCall)]
    public class AuthenticationServiceBehavior : IAuthenticationServiceContract
    {
    }
}
