using SanteDB.Client.OAuth;
using SanteDB.Client.Services;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream
{
    [PreferredService(typeof(IApplicationIdentityProviderService))]
    public class UpstreamApplicationIdentityProvider : IApplicationIdentityProviderService
    {
        IApplicationIdentityProviderService _LocalApplicationIdentityProvider;
        IOAuthClient _OAuthClient;

        public UpstreamApplicationIdentityProvider(IOAuthClient oauthClient, ILocalApplicationIdentityProviderService localApplicationIdentityProvider = null)
        {
            _LocalApplicationIdentityProvider = localApplicationIdentityProvider;
            _OAuthClient = oauthClient ?? throw new ArgumentNullException(nameof(oauthClient));
        }

        public string ServiceName => "Upstream Application Identity Provider";

        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        protected virtual bool ShouldDoRemoteAuthentication(string clientId) => true;

        private void SynchronizeLocalIdentity(IClaimsPrincipal remoteIdentity)
        {

        }

        public void AddClaim(string applicationName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            if(_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }
            _LocalApplicationIdentityProvider.AddClaim(applicationName, claim, principal, expiry);
        }

        public IPrincipal Authenticate(string clientId, string clientSecret)
        {
            if (ShouldDoRemoteAuthentication(clientId))
            {
                try
                {
                    var result = _OAuthClient.AuthenticateApp(clientId, clientSecret);

                    SynchronizeLocalIdentity(result);

                    return result;
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {

                }
            }
            else if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.Authenticate(clientId, clientSecret);
        }



        public IPrincipal Authenticate(string clientId, IPrincipal authenticationContext)
        {
            if (ShouldDoRemoteAuthentication(clientId))
            {
                try
                {
                    var result = _OAuthClient.AuthenticateApp(clientId);

                    SynchronizeLocalIdentity(result);

                    return result;
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {

                }
            }
            else if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.Authenticate(clientId, authenticationContext);
        }

        public void ChangeSecret(string applicationName, string secret, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            _LocalApplicationIdentityProvider.ChangeSecret(applicationName, secret, principal);
        }

        public IApplicationIdentity CreateIdentity(string applicationName, string password, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            var identity = _LocalApplicationIdentityProvider.CreateIdentity(applicationName, password, principal);

            return identity;
        }

        public IEnumerable<IClaim> GetClaims(string applicationName)
        {
            if (ShouldDoRemoteAuthentication(applicationName))
            {
                try
                {
                    var remoteprincipal = _OAuthClient.AuthenticateApp(applicationName);

                    SynchronizeLocalIdentity(remoteprincipal);

                    return remoteprincipal.Claims;
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {

                }
            }
            else if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.GetClaims(applicationName);
        }

        public IApplicationIdentity GetIdentity(string applicationName)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.GetIdentity(applicationName);
        }

        public byte[] GetPublicSigningKey(string applicationName)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.GetPublicSigningKey(applicationName);
        }

        public Guid GetSid(string name)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.GetSid(name);
        }

        public void RemoveClaim(string applicationName, string claimType, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            _LocalApplicationIdentityProvider.RemoveClaim(applicationName, claimType, principal);
        }

        public void SetLockout(string applicationName, bool lockoutState, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            _LocalApplicationIdentityProvider.SetLockout(applicationName, lockoutState, principal);
        }

        public void SetPublicKey(string applicationName, byte[] key, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            _LocalApplicationIdentityProvider.SetPublicKey(applicationName, key, principal);
        }

        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal is OAuthClaimsPrincipal oacp && oacp.CanRefresh)
            {
                var result = _OAuthClient.Refresh(oacp.GetRefreshToken());

                SynchronizeLocalIdentity(result);

                return result;
            }
            else if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }
            else
            {
                throw new NotSupportedException("Cannot refresh this principal");
            }

        }
    }
}
