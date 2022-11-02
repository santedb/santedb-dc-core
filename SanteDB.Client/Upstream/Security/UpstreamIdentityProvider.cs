using SanteDB.Client.OAuth;
using SanteDB.Client.Services;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Security
{
    [PreferredService(typeof(IIdentityProviderService))]
    public class UpstreamIdentityProvider : IIdentityProviderService
    {
        readonly IIdentityProviderService _LocalIdentityProvider;
        readonly IOAuthClient _OAuthClient;

        public UpstreamIdentityProvider(
            IOAuthClient oauthClient,
            ILocalIdentityProviderService localIdentityProvider = null) // on initial configuration in online only mode there are no local users
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
            if (_LocalIdentityProvider == null) // If this is online only configured - local not allowed
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }

            _LocalIdentityProvider?.AddClaim(userName, claim, principal, expiry);
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
            else if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
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
            else if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
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
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.CreateIdentity(userName, password, principal);
        }

        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.DeleteIdentity(userName, principal);
        }

        public AuthenticationMethod GetAuthenticationMethods(string userName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IClaim> GetClaims(string userName)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.GetClaims(userName);
        }

        public IIdentity GetIdentity(string userName)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.GetIdentity(userName);
        }

        public IIdentity GetIdentity(Guid sid)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.GetIdentity(sid);
        }

        public Guid GetSid(string name)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
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
            else if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            else
            {
                return _LocalIdentityProvider.ReAuthenticate(principal);
            }
        }

        public void RemoveClaim(string userName, string claimType, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.RemoveClaim(userName, claimType, principal);
        }

        public void SetLockout(string userName, bool lockout, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.SetLockout(userName, lockout, principal);
        }
    }
}
