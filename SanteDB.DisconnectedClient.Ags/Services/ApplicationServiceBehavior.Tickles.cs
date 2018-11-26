using RestSrvr;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Core.Tickler;
using SanteDB.DisconnectedClient.Xamarin.Services.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// Application service behavior for tickles
    /// </summary>
    public partial class ApplicationServiceBehavior
    {
        /// <summary>
        /// Create a new tickle (allows applications to tickle the user)
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public void CreateTickle(Tickle data)
        {
            ApplicationContext.Current.GetService<ITickleService>()?.SendTickle(data);
            RestOperationContext.Current.OutgoingResponse.StatusCode = 201;
        }

        /// <summary>
        /// Delete the tickle
        /// </summary>
        public void DeleteTickle(Guid id)
        {
            ApplicationContext.Current.GetService<ITickleService>()?.DismissTickle(id);
        }

        /// <summary>
        /// Get all tickles
        /// </summary>
        public List<Tickle> GetTickles()
        {
            var suser = ApplicationContext.Current.GetService<ISecurityRepositoryService>().GetUser(AuthenticationContext.Current.Principal.Identity);
            return ApplicationContext.Current.GetService<ITickleService>()?.GetTickles(o => o.Expiry > DateTime.Now && (o.Target == Guid.Empty || o.Target == suser.Key)).ToList();
        }

    }
}
