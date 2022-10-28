using SanteDB.Client.Services;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream
{
    //[PreferredService(typeof(IIdentityProviderService))]
    public class UpstreamIdentityProvider : IIdentityProviderService
    {
        readonly IIdentityProviderService _LocalIdentityProvider;
        readonly IOAuthClient _OAuthClient;

        public UpstreamIdentityProvider(IIdentityProviderService localIdentityProvider, IOAuthClient oauthClient)
        {
            _LocalIdentityProvider = localIdentityProvider;
            _OAuthClient = oauthClient;
        }

        public string ServiceName => "Upstream Identity Provider";

        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        public void AddClaim(string userName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            throw new NotImplementedException();
        }

        public IPrincipal Authenticate(string userName, string password)
        {
            throw new NotImplementedException();
        }

        public IPrincipal Authenticate(string userName, string password, string tfaSecret)
        {
            throw new NotImplementedException();
        }

        public void ChangePassword(string userName, string newPassword, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public IIdentity CreateIdentity(string userName, string password, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public AuthenticationMethod GetAuthenticationMethods(string userName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IClaim> GetClaims(string userName)
        {
            throw new NotImplementedException();
        }

        public IIdentity GetIdentity(string userName)
        {
            throw new NotImplementedException();
        }

        public IIdentity GetIdentity(Guid sid)
        {
            throw new NotImplementedException();
        }

        public Guid GetSid(string name)
        {
            throw new NotImplementedException();
        }

        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public void RemoveClaim(string userName, string claimType, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public void SetLockout(string userName, bool lockout, IPrincipal principal)
        {
            throw new NotImplementedException();
        }
    }
}
