using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// A security challenge service that operates locally
    /// </summary>
    public interface IOfflineSecurityChallengeService : ISecurityChallengeService
    {
    }
}
