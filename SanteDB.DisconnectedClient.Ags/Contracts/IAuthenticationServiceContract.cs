using RestSrvr.Attributes;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Xamarin.Security;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Contracts
{
    /// <summary>
    /// Authentication service contract
    /// </summary>
    [ServiceContract(Name = "AUTH")]
    public interface IAuthenticationServiceContract
    {

        /// <summary>
        /// Authenticate the user
        /// </summary>
        [Post("oauth2_token")]
        SessionInfo Authenticate(NameValueCollection request);

        /// <summary>
        /// Get the session
        /// </summary>
        [Get("session")]
        SessionInfo GetSession();

        /// <summary>
        /// Delete (abandon) the session
        /// </summary>
        [Delete("session")]
        void AbandonSession();
    }
}
