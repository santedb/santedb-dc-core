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
    public class UpstreamApplicationIdentityProvider : UpstreamServiceBase, IApplicationIdentityProviderService, IUpstreamServiceProvider<IApplicationIdentityProviderService>
    {
        readonly ILocalServiceProvider<IApplicationIdentityProviderService> _LocalApplicationIdentityProvider;
        readonly IOAuthClient _OAuthClient;
        readonly ILocalizationService _LocalizationService;
        readonly IPolicyInformationService _RemotePolicyInformationService;
        readonly ILocalServiceProvider<IPolicyInformationService> _LocalPolicyInformationService;
        readonly IRoleProviderService _RemoteRoleProviderService;
        readonly ILocalServiceProvider<IRoleProviderService> _LocalRoleProviderService;

        readonly bool _CanSyncPolicies;
        readonly bool _CanSyncRoles;

        /// <inheritdoc/>
        public IApplicationIdentityProviderService UpstreamProvider => this;
        
        public UpstreamApplicationIdentityProvider(
            IOAuthClient oauthClient,
            IRestClientFactory restClientFactory,
            ILocalizationService localizationService,
            IUpstreamManagementService upstreamManagementService,
            IPolicyInformationService remotePolicyInformationService,
            IRoleProviderService remoteRoleProviderService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService,
            ILocalServiceProvider<IPolicyInformationService> localPolicyInformationService = null,
            ILocalServiceProvider<IRoleProviderService> localRoleProviderService = null,
            ILocalServiceProvider<IApplicationIdentityProviderService> localIdentityProvider = null) // on initial configuration in online only mode there are no local users
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
            var localapp = _LocalApplicationIdentityProvider.LocalProvider.GetIdentity(clientId);

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

            var localapp = _LocalApplicationIdentityProvider.LocalProvider.GetIdentity(appidentity.Name);

            if (null == localapp)
            {
                localapp = _LocalApplicationIdentityProvider.LocalProvider.CreateIdentity(appidentity.Name, clientSecret, AuthenticationContext.SystemPrincipal);
            }
            else if (!string.IsNullOrEmpty(clientSecret))
            {
                _LocalApplicationIdentityProvider.LocalProvider.ChangeSecret(appidentity.Name, clientSecret, AuthenticationContext.SystemPrincipal);
            }

            var policiestoadd = new List<IPolicy>();
            var policiestogrant = new List<(PolicyGrantType rule, string policyOid)>();

            using (AuthenticationContext.EnterContext(remoteIdentity))
            {
                if (_CanSyncPolicies)
                {
                    foreach (var policyoid in policiestosync)
                    {
                        var localpolicy = _LocalPolicyInformationService.LocalProvider.GetPolicy(policyoid);

                        if (null == localpolicy)
                        {
                            var remotepolicy = _RemotePolicyInformationService.GetPolicy(policyoid);
                            policiestoadd.Add(remotepolicy);
                        }
                    }

                    var remotepolicies = _RemotePolicyInformationService.GetPolicies(appidentity);

                    foreach (var remotepolicy in remotepolicies)
                    {
                        if (null == _LocalPolicyInformationService.LocalProvider.GetPolicy(remotepolicy.Policy.Oid))
                        {
                            policiestoadd.Add(remotepolicy.Policy);
                        }

                        policiestogrant.Add((remotepolicy.Rule, remotepolicy.Policy.Oid));
                    }
                }
            }

            if (policiestoadd.Count > 0 || policiestoadd.Count > 0)
            {
                using (AuthenticationContext.EnterSystemContext())
                {
                    foreach (var policytoadd in policiestoadd)
                    {
                        _LocalPolicyInformationService.LocalProvider.CreatePolicy(policytoadd, AuthenticationContext.SystemPrincipal);
                    }

                    foreach (var policytogrant in policiestogrant)
                    {
                        _LocalPolicyInformationService.LocalProvider.AddPolicies(localapp, policytogrant.rule, AuthenticationContext.SystemPrincipal, policytogrant.policyOid);
                    }
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
            _LocalApplicationIdentityProvider.LocalProvider.AddClaim(applicationName, claim, principal, expiry);
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
                _Tracer.TraceVerbose("Authenticating Event signals cancel.");
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

                result = _LocalApplicationIdentityProvider.LocalProvider.Authenticate(clientId, clientSecret) as IClaimsPrincipal;

                return result;
            }
            finally
            {
                Authenticated?.Invoke(this, new AuthenticatedEventArgs(clientId, result, null != result));
            }
        }

        public void ChangeSecret(string applicationName, string secret, IPrincipal principal)
        {
            _LocalApplicationIdentityProvider?.LocalProvider.ChangeSecret(applicationName, secret, principal);
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

            var identity = _LocalApplicationIdentityProvider.LocalProvider.CreateIdentity(applicationName, password, principal);

            _LocalApplicationIdentityProvider.LocalProvider.AddClaim(applicationName, new SanteDBClaim(SanteDBClaimTypes.LocalOnly, "true"), principal);

            identity = _LocalApplicationIdentityProvider.LocalProvider.GetIdentity(applicationName);

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

            return _LocalApplicationIdentityProvider.LocalProvider.GetClaims(applicationName);
        }

        public IApplicationIdentity GetIdentity(string applicationName)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.LocalProvider.GetIdentity(applicationName);
        }

        public byte[] GetPublicSigningKey(string applicationName)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.LocalProvider.GetPublicSigningKey(applicationName);
        }

        public Guid GetSid(string name)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            return _LocalApplicationIdentityProvider.LocalProvider.GetSid(name);
        }

        public void RemoveClaim(string applicationName, string claimType, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            _LocalApplicationIdentityProvider.LocalProvider.RemoveClaim(applicationName, claimType, principal);
        }

        public void SetLockout(string applicationName, bool lockoutState, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            _LocalApplicationIdentityProvider.LocalProvider.SetLockout(applicationName, lockoutState, principal);
        }

        public void SetPublicKey(string applicationName, byte[] key, IPrincipal principal)
        {
            if (_LocalApplicationIdentityProvider == null)
            {
                throw new InvalidOperationException(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED);
            }

            _LocalApplicationIdentityProvider.LocalProvider.SetPublicKey(applicationName, key, principal);
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
