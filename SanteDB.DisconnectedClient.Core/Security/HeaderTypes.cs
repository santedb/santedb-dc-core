using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security
{
    public static class HeaderTypes
    {

        public const string HttpDeviceAuthentication = "X-Device-Authorization";

        public const string HttpClaims = "X-SanteDBClient-Claim";

        public const string HttpTfaSecret = "X-SanteDB-TfaSecret";

        public const string HttpUserAccessControlPrompt = "X-SanteDBClient-UserAccessControl";
    }
}
