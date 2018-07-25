using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Xamarin.Security;
using SanteDB.DisconnectedClient.Xamarin.Services.Attributes;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Model.Collection;
using SanteDB.DisconnectedClient.Xamarin.Services.Model;

namespace SanteDB.DisconnectedClient.Xamarin.Services.ServiceHandlers
{
    /// <summary>
    /// Represents a service for the AMI
    /// </summary>
    [RestService("/__ami")]
    public class AmiService
    {

        /// <summary>
        /// Update the security user
        /// </summary>
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        [RestOperation(UriPath = "/SecurityUser", Method = "POST", FaultProvider = nameof(AmiFaultProvider))]
        public Object UpdateSecurityUser([RestMessage(RestMessageFormat.SimpleJson)] SecurityUserInfo user)
        {
            var localSecSrv = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
            var amiServ = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));

            if (user.PasswordOnly)
            {
                var idp = ApplicationContext.Current.GetService<IIdentityProviderService>();
                idp.ChangePassword(user.Entity.UserName.ToLower(), user.Entity.Password, AuthenticationContext.Current.Principal);
                return AuthenticationContext.Current.Session;
            }
            else
            {
                // Session
                amiServ.Client.Credentials = new TokenCredentials(AuthenticationContext.Current.Principal);
                var remoteUser = amiServ.GetUser(user.Entity.Key.ToString());
                remoteUser.Entity.Email = user.Entity.Email;
                remoteUser.Entity.PhoneNumber = user.Entity.PhoneNumber;
                // Save the remote user in the local
                localSecSrv.SaveUser(remoteUser.Entity);
                amiServ.UpdateUser(remoteUser.Entity.Key.Value, remoteUser);
                return remoteUser.Entity;
            }
        }

        /// <summary>
        /// Gets a user by username.
        /// </summary>
        /// <param name="username">The username of the user to be retrieved.</param>
        /// <returns>Returns the user.</returns>
        [RestOperation(Method = "GET", UriPath = "/SecurityUser")]
        [return: RestMessage(RestMessageFormat.Json)]
        public IdentifiedData GetUser()
        {
            // this is used for the forgot password functionality
            // need to find a way to stop people from simply searching users via username...

            NameValueCollection query = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
            var predicate = QueryExpressionParser.BuildLinqExpression<SecurityUser>(query);
            ISecurityRepositoryService securityRepositoryService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

            if (query.ContainsKey("_id"))
                return securityRepositoryService.GetUser(Guid.Parse(query["_id"][0]));
            else
                return Bundle.CreateBundle(securityRepositoryService.FindUsers(predicate), 0, 0);
        }

         /// <summary>
        /// Care plan fault provider
        /// </summary>
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public ErrorResult AmiFaultProvider(Exception e)
        {
            return new ErrorResult() { Error = e.Message, ErrorDescription = e.InnerException?.Message, ErrorType = e.GetType().Name };
        }
    }
}
