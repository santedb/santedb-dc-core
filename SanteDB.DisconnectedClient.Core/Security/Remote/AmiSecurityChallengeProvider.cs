using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Core.Services.Remote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security.Remote
{
    /// <summary>
    /// Security challenge provider which fetches a user's challenge responses from a server
    /// </summary>
    public class AmiSecurityChallengeProvider : AmiRepositoryBaseService, ISecurityChallengeService
    {
        /// <summary>
        /// Gets the service name for the security challenge service
        /// </summary>
        public string ServiceName => "Remote Security Challenge Service";

        /// <summary>
        /// Get the security challenges from the server (actually typically 
        /// </summary>
        public IEnumerable<SecurityChallenge> Get(string userName, IPrincipal principal)
        {
            using(var client = this.GetClient())
            {
                // Get the user name
                var user = client.GetUsers(o => o.UserName == userName).CollectionItem.OfType<SecurityUser>().FirstOrDefault();
                if (user == null)
                    throw new KeyNotFoundException($"Error fetching client challenges");
                return client.Client.Get<AmiCollection>($"SecurityUser/{user.Key}/challenge").CollectionItem.OfType<SecurityChallenge>();
            }
        }

        /// <summary>
        /// Remove a security challenge for the user
        /// </summary>
        public void Remove(string userName, Guid challengeKey, IPrincipal principal)
        {
            using (var client = this.GetClient(principal))
            {
                // CLIENT MUST BE USING THEIR OWN CREDENTIAL
                var user = client.GetUsers(o => o.UserName == userName).CollectionItem.OfType<SecurityUser>().FirstOrDefault();
                if (user == null)
                    throw new KeyNotFoundException($"Error removing client challenge");
                client.Client.Delete<SecurityChallenge>($"SecurityUser/{user.Key}/challenge/{challengeKey}");
            }
        }

        /// <summary>
        /// Set the answer to a challenge response
        /// </summary>
        public void Set(string userName, Guid challengeKey, string response, IPrincipal principal)
        {
            using (var client = this.GetClient(principal))
            {
                // CLIENT MUST BE USING THEIR OWN CREDENTIAL
                var user = client.GetUsers(o => o.UserName == userName).CollectionItem.OfType<SecurityUser>().FirstOrDefault();
                if (user == null)
                    throw new KeyNotFoundException($"Error setting challenge");

                var challengeSet = new SecurityUserChallengeInfo()
                {
                    ChallengeKey = challengeKey,
                    ChallengeResponse = response
                };
                client.Client.Post<SecurityUserChallengeInfo, Object>($"SecurityUser/{user.Key}/challenge", client.Client.Accept, challengeSet);
            }
        }
    }
}
