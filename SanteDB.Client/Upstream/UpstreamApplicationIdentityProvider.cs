using SanteDB.Client.Services;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream
{
    //[PreferredService(typeof(IApplicationIdentityProviderService))]
    public class UpstreamApplicationIdentityProvider : IApplicationIdentityProviderService
    {
        IApplicationIdentityProviderService _LocalApplicationIdentityProvider;
        IOAuthClient _OAuthClient;

        public UpstreamApplicationIdentityProvider(IApplicationIdentityProviderService localApplicationIdentityProvider, IOAuthClient oauthClient)
        {
            _LocalApplicationIdentityProvider = localApplicationIdentityProvider;
            _OAuthClient = oauthClient;
        }

        public string ServiceName => "Upstream Application Identity Provider";

        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        public void AddClaim(string applicationName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            throw new NotImplementedException();
        }

        public IPrincipal Authenticate(string applicationName, string applicationSecret)
        {
            throw new NotImplementedException();
        }

        public IPrincipal Authenticate(string applicationName, IPrincipal authenticationContext)
        {
            throw new NotImplementedException();
        }

        public void ChangeSecret(string applicationName, string secret, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public IApplicationIdentity CreateIdentity(string applicationName, string password, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IClaim> GetClaims(string applicationName)
        {
            throw new NotImplementedException();
        }

        public IApplicationIdentity GetIdentity(string applicationName)
        {
            throw new NotImplementedException();
        }

        public byte[] GetPublicSigningKey(string applicationName)
        {
            throw new NotImplementedException();
        }

        public Guid GetSid(string name)
        {
            throw new NotImplementedException();
        }

        public void RemoveClaim(string applicationName, string claimType, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public void SetLockout(string applicationName, bool lockoutState, IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public void SetPublicKey(string applicationName, byte[] key, IPrincipal principal)
        {
            throw new NotImplementedException();
        }
    }
}
