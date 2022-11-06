using SanteDB.Client.Exceptions;
using SanteDB.Client.OAuth;
using SanteDB.Client.Repositories;
using SanteDB.Client.Services;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Security
{
    [PreferredService(typeof(IApplicationIdentityProviderService))]
    public class UpstreamApplicationIdentityProvider : UpstreamServiceBase, IApplicationIdentityProviderService
    {
        readonly ILocalApplicationIdentityProviderService _LocalApplicationIdentityProvider;
        readonly IOAuthClient _OAuthClient;
        readonly ILocalizationService _LocalizationService;
        readonly IPolicyInformationService _RemotePolicyInformationService;
        readonly ILocalPolicyInformationService _LocalPolicyInformationService;
        readonly IRoleProviderService _RemoteRoleProviderService;
        readonly ILocalRoleProviderService _LocalRoleProviderService;

        readonly bool _CanSyncPolicies;
        readonly bool _CanSyncRoles;

        public UpstreamApplicationIdentityProvider(
            IOAuthClient oauthClient,
            IRestClientFactory restClientFactory,
            ILocalizationService localizationService,
            IUpstreamManagementService upstreamManagementService,
            IPolicyInformationService remotePolicyInformationService,
            IRoleProviderService remoteRoleProviderService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService,
            ILocalPolicyInformationService localPolicyInformationService = null,
            ILocalRoleProviderService localRoleProviderService = null,
            ILocalApplicationIdentityProviderService localIdentityProvider = null) // on initial configuration in online only mode there are no local users
            : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            _LocalApplicationIdentityProvider = localIdentityProvider;
            _LocalizationService = localizationService;
            _RemotePolicyInformationService = remotePolicyInformationService;
            _RemoteRoleProviderService = remoteRoleProviderService;
            _LocalRoleProviderService = localRoleProviderService;
            _LocalPolicyInformationService = localPolicyInformationService;
            _OAuthClient = oauthClient ?? throw new ArgumentNullException(nameof(oauthClient));

            _CanSyncPolicies = null != _LocalPolicyInformationService && _RemotePolicyInformationService != _LocalPolicyInformationService;
            _CanSyncRoles = null != _LocalRoleProviderService && _RemoteRoleProviderService != _LocalRoleProviderService;
        }

        public string ServiceName => "Upstream Application Identity Provider";

        /// <inheritdoc />
        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        /// <inheritdoc />
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        protected virtual bool ShouldDoRemoteAuthentication(string clientId)
        {
            //Null provider so we are in online or initial mode.
            if (null == _LocalApplicationIdentityProvider)
            {
                return true;
            }

            //Look for an app marked local only.
            var localapp = _LocalApplicationIdentityProvider.GetIdentity(clientId);

            if (localapp is IClaimsIdentity icl)
            {
                if (icl.FindFirst(SanteDBClaimTypes.LocalOnly) != null)
                {
                    return false;
                }
            }

            //TODO: Check remote connectivity with upstream provider.

            //Default to true.
            return IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AuthenticationService);
        }

        private void SynchronizeLocalIdentity(IClaimsPrincipal remoteIdentity, string clientSecret)
        {
            if (null == remoteIdentity)
            {
                throw new ArgumentNullException(nameof(remoteIdentity), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (null == _LocalApplicationIdentityProvider) //In online-only mode or initial mode, there will not be a local provider.
            {
                return;
            }

            var appidentity = remoteIdentity.Identity as IClaimsIdentity; //Coming from OAuth, there will always be one that represents the app.

            var policiestosync = new List<string>();
            var rolestosync = new List<string>();
            string appid = null;

            policiestosync.AddRange(remoteIdentity.Claims.Where(c => c.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(c => c.Value));
            rolestosync.AddRange(remoteIdentity.Claims.Where(c => c.Type == SanteDBClaimTypes.DefaultRoleClaimType).Select(c => c.Value));

            if (null != appidentity)
            {
                policiestosync.AddRange(appidentity.Claims.Where(c => c.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(c => c.Value));
                rolestosync.AddRange(appidentity.Claims.Where(c => c.Type == SanteDBClaimTypes.DefaultRoleClaimType).Select(c => c.Value));

                appid = appidentity.GetFirstClaimValue(SanteDBClaimTypes.SanteDBApplicationIdentifierClaim, SanteDBClaimTypes.SecurityId);
            }

            var localapp = _LocalApplicationIdentityProvider.GetIdentity(appidentity.Name);

            if (null == localapp)
            {
                localapp = _LocalApplicationIdentityProvider.CreateIdentity(appidentity.Name, clientSecret, AuthenticationContext.SystemPrincipal);
            }
            else if (!string.IsNullOrEmpty(clientSecret))
            {
                _LocalApplicationIdentityProvider.ChangeSecret(appidentity.Name, clientSecret, AuthenticationContext.SystemPrincipal);
            }

            if (_CanSyncPolicies)
            {
                foreach (var policyoid in policiestosync)
                {
                    var localpolicy = _LocalPolicyInformationService.GetPolicy(policyoid);

                    if (null == localpolicy)
                    {
                        var remotepolicy = _RemotePolicyInformationService.GetPolicy(policyoid);
                        _LocalPolicyInformationService.CreatePolicy(remotepolicy, AuthenticationContext.SystemPrincipal);
                    }
                }

                var remotepolicies = _RemotePolicyInformationService.GetPolicies(appidentity);

                foreach(var remotepolicy in remotepolicies)
                {
                    if (null == _LocalPolicyInformationService.GetPolicy(remotepolicy.Policy.Oid))
                    {
                        _LocalPolicyInformationService.CreatePolicy(remotepolicy.Policy, AuthenticationContext.SystemPrincipal);
                    }

                    _LocalPolicyInformationService.AddPolicies(localapp, remotepolicy.Rule, AuthenticationContext.SystemPrincipal, remotepolicy.Policy.Oid);
                }
            }
        }

        public void ChangeRemoteSecret(string clientId, string clientSecret)
        {
            using(var amiclient = CreateAmiServiceClient())
            {
                var remoteapp = amiclient.GetApplications(app => app.Name == clientId)?.CollectionItem?.OfType<SecurityApplicationInfo>()?.FirstOrDefault();

                if (null == remoteapp?.Key)
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(SecurityApplication) }));
                }

                remoteapp.Entity.ApplicationSecret = clientSecret;

                try
                {
                    amiclient.UpdateApplication(remoteapp.Entity.Key.Value, remoteapp);
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = remoteapp.Entity.Key.Value }), ex);
                }
            }
        }

        public void AddClaim(string applicationName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }
            _LocalApplicationIdentityProvider.AddClaim(applicationName, claim, principal, expiry);
        }

        public IPrincipal Authenticate(string clientId, string clientSecret)
            => AuthenticateInternal(clientId, clientSecret);



        public IPrincipal Authenticate(string clientId, IPrincipal authenticationContext)
            => AuthenticateInternal(clientId, null, authenticationContext);

        private IPrincipal AuthenticateInternal(string clientId, string clientSecret = null, IPrincipal authenticationContext = null)
        {
            var authenticatingargs = new AuthenticatingEventArgs(clientId);
            Authenticating?.Invoke(this, authenticatingargs);

            if (authenticatingargs.Cancel)
            {
                m_tracer.TraceVerbose("Authenticating Event signals cancel.");
                if (authenticatingargs.Success)
                {
                    return authenticatingargs.Principal;
                }
                else
                {
                    throw new AuthenticationException(_LocalizationService.GetString(ErrorMessageStrings.AUTH_CANCELLED));
                }
            }

            IClaimsPrincipal result = null;

            try
            {
                if (ShouldDoRemoteAuthentication(clientId))
                {
                    try
                    {
                        if (null != clientSecret)
                        {
                            result = _OAuthClient.AuthenticateApp(clientId, clientSecret);
                        }
                        else if (null != authenticationContext)
                        {
                            using (AuthenticationContext.EnterContext(authenticationContext)) // Enter authentication context so the rest client knows to append the proper headers
                            {
                                result = _OAuthClient.AuthenticateApp(clientId);
                            }
                        }

                        SynchronizeLocalIdentity(result, clientSecret);

                        return result;
                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
                    }
                }
                else if (_LocalApplicationIdentityProvider == null)
                {
                    throw new UpstreamIntegrationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
                }

                result = _LocalApplicationIdentityProvider.Authenticate(clientId, clientSecret) as IClaimsPrincipal;

                return result;
            }
            finally
            {
                Authenticated?.Invoke(this, new AuthenticatedEventArgs(clientId, result, null != result));
            }
        }

        public void ChangeSecret(string applicationName, string secret, IPrincipal principal)
        {
            _LocalApplicationIdentityProvider?.ChangeSecret(applicationName, secret, principal);
            if (ShouldDoRemoteAuthentication(applicationName))
            {
                ChangeRemoteSecret(applicationName, secret);
            }
        }

        public IApplicationIdentity CreateIdentity(string applicationName, string password, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            var identity = _LocalApplicationIdentityProvider.CreateIdentity(applicationName, password, principal);

            _LocalApplicationIdentityProvider.AddClaim(applicationName, new SanteDBClaim(SanteDBClaimTypes.LocalOnly, "true"), principal);

            identity = _LocalApplicationIdentityProvider.GetIdentity(applicationName);

            return identity;
        }

        public IEnumerable<IClaim> GetClaims(string applicationName)
        {
            if (ShouldDoRemoteAuthentication(applicationName))
            {
                try
                {
                    var remoteprincipal = _OAuthClient.AuthenticateApp(applicationName);

                    SynchronizeLocalIdentity(remoteprincipal, null);

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

                SynchronizeLocalIdentity(result, null);

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
