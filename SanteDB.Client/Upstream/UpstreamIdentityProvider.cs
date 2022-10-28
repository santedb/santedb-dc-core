using SanteDB.Client.OAuth;
using SanteDB.Client.Services;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream
{
    [PreferredService(typeof(IIdentityProviderService))]
    public class UpstreamIdentityProvider : IIdentityProviderService
    {
        readonly IIdentityProviderService _LocalIdentityProvider;
        readonly IOAuthClient _OAuthClient;

        public UpstreamIdentityProvider(IIdentityProviderService localIdentityProvider, IOAuthClient oauthClient)
        {
            _LocalIdentityProvider = localIdentityProvider;
            _OAuthClient = oauthClient ?? throw new ArgumentNullException(nameof(oauthClient));
        }

        public string ServiceName => "Upstream Identity Provider";

        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        public bool ShouldDoRemoteAuthentication(string userName) => true;

        public void SynchronizeLocalIdentity(IClaimsPrincipal remoteIdentity)
        {

        }

        public void ChangeRemotePassword(string username, string newPassword)
        {

        }

        public void AddClaim(string userName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            _LocalIdentityProvider.AddClaim(userName, claim, principal, expiry);
        }

        public IPrincipal Authenticate(string userName, string password)
        {
            if (ShouldDoRemoteAuthentication(userName))
            {
                try
                {
                    var result = _OAuthClient.AuthenticateUser(userName, password);

                    SynchronizeLocalIdentity(result);

                    return result;
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {

                }
            }

            return _LocalIdentityProvider.Authenticate(userName, password);
        }

        public IPrincipal Authenticate(string userName, string password, string tfaSecret)
        {
            if (ShouldDoRemoteAuthentication(userName))
            {
                try
                {
                    var result = _OAuthClient.AuthenticateUser(userName, password);

                    SynchronizeLocalIdentity(result);

                    return result;
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {

                }
            }

            return _LocalIdentityProvider.Authenticate(userName, password, tfaSecret);
        }

        public void ChangePassword(string userName, string newPassword, IPrincipal principal)
        {
            _LocalIdentityProvider.ChangePassword(userName, newPassword, principal);
            if (ShouldDoRemoteAuthentication(userName))
            {
                ChangeRemotePassword(userName, newPassword);
            }
        }

        public IIdentity CreateIdentity(string userName, string password, IPrincipal principal)
        {
            return _LocalIdentityProvider.CreateIdentity(userName, password, principal);
        }

        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            _LocalIdentityProvider.DeleteIdentity(userName, principal);
        }

        public AuthenticationMethod GetAuthenticationMethods(string userName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IClaim> GetClaims(string userName)
        {
            return _LocalIdentityProvider.GetClaims(userName);
        }

        public IIdentity GetIdentity(string userName)
        {
            return _LocalIdentityProvider.GetIdentity(userName);
        }

        public IIdentity GetIdentity(Guid sid)
        {
            return _LocalIdentityProvider.GetIdentity(sid);
        }

        public Guid GetSid(string name)
        {
            return _LocalIdentityProvider.GetSid(name);
        }

        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal is OAuthClaimsPrincipal oacp)
            {
                if (oacp.CanRefresh)
                {
                    var result = _OAuthClient.Refresh(oacp.GetRefreshToken());

                    SynchronizeLocalIdentity(result);

                    return result;
                }
                else
                {
                    throw new NotSupportedException("Cannot refresh this principal");
                }
            }
            else
            {
                return _LocalIdentityProvider.ReAuthenticate(principal);
            }
        }

        public void RemoveClaim(string userName, string claimType, IPrincipal principal)
        {
            _LocalIdentityProvider.RemoveClaim(userName, claimType, principal);
        }

        public void SetLockout(string userName, bool lockout, IPrincipal principal)
        {
            _LocalIdentityProvider.SetLockout(userName, lockout, principal);
        }
    }
}
