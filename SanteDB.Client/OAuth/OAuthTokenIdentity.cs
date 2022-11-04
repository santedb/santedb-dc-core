using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.OAuth;
using SanteDB.Core.Security.Principal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace SanteDB.Client.OAuth
{
    public class OAuthTokenIdentity : IClaimsIdentity
    {
        readonly SecurityToken _Token;

        readonly List<IClaim> _Claims;


        public OAuthTokenIdentity(SecurityToken token, string authenticationType, bool isAuthenticated, List<IClaim> claims)
        {
            _Token = token;
            AuthenticationType = authenticationType;
            IsAuthenticated = isAuthenticated;
            _Claims = claims;
        }

        public IEnumerable<IClaim> Claims => _Claims;

        public string AuthenticationType { get; }

        public bool IsAuthenticated { get; }

        public string Name => FindFirst(SanteDBClaimTypes.Name)?.Value;


        public IEnumerable<IClaim> FindAll(string claimType) => _Claims.Where(c => c.Type == claimType);

        public IClaim FindFirst(string claimType) => _Claims.FirstOrDefault(c => c.Type == claimType);
    }
}
