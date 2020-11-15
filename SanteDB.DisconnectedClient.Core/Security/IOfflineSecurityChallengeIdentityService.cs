using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents a security challenge identity service that operates only offline
    /// </summary>
    public interface IOfflineSecurityChallengeIdentityService : ISecurityChallengeIdentityService
    {
    }
}
