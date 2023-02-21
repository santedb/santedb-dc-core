using SanteDB.Client.Exceptions;
using SanteDB.Client.OAuth;
using SanteDB.Client.Repositories;
using SanteDB.Client.Services;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Rest.AMI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Security
{
    [PreferredService(typeof(IIdentityProviderService))]
    public class UpstreamIdentityProvider : UpstreamServiceBase, IIdentityProviderService
    {
        readonly ILocalServiceProvider<IIdentityProviderService> _LocalIdentityProvider;
        readonly IOAuthClient _OAuthClient;
        readonly ILocalizationService _LocalizationService;
        readonly IUpstreamServiceProvider<IPolicyInformationService> _RemotePolicyInformationService;
        readonly ILocalServiceProvider<IPolicyInformationService> _LocalPolicyInformationService;
        readonly IRoleProviderService _RemoteRoleProviderService;
        readonly ILocalServiceProvider<IRoleProviderService> _LocalRoleProviderService;
        readonly ISecurityRepositoryService _SecurityRepositoryService;

        readonly bool _CanSyncPolicies;
        readonly bool _CanSyncRoles;

        public UpstreamIdentityProvider(
            IOAuthClient oauthClient,
            IRestClientFactory restClientFactory,
            ILocalizationService localizationService,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamServiceProvider<IPolicyInformationService> remotePolicyInformationService,
            IRoleProviderService remoteRoleProviderService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            ISecurityRepositoryService securityRepositoryService,
            ILocalServiceProvider<IPolicyInformationService> localPolicyInformationService = null,
            ILocalServiceProvider<IRoleProviderService> localRoleProviderService = null,
            ILocalServiceProvider<IIdentityProviderService> localIdentityProvider = null) // on initial configuration in online only mode there are no local users
            : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider)
        {
            _LocalIdentityProvider = localIdentityProvider;
            _LocalizationService = localizationService;
            _RemotePolicyInformationService = remotePolicyInformationService;
            _RemoteRoleProviderService = remoteRoleProviderService;
            _LocalRoleProviderService = localRoleProviderService;
            _SecurityRepositoryService = securityRepositoryService;
            _LocalPolicyInformationService = localPolicyInformationService;
            _OAuthClient = oauthClient ?? throw new ArgumentNullException(nameof(oauthClient));
            _CanSyncPolicies = null != _LocalPolicyInformationService && _RemotePolicyInformationService != _LocalPolicyInformationService;
            _CanSyncRoles = null != _LocalRoleProviderService && _RemoteRoleProviderService != _LocalRoleProviderService;
        }

        public string ServiceName => "Upstream Identity Provider";

        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        public bool ShouldDoRemoteAuthentication(string userName)
        {
            //Null local provider so we're in online-only or initial mode.
            if (null == _LocalIdentityProvider || !IsUpstreamConfigured)
            {
                return true;
            }

            //Try to check if the local user is marked as local only.
            var localuser = _LocalIdentityProvider.LocalProvider.GetIdentity(userName);

            if (localuser is IClaimsIdentity icl)
            {
                if (icl.Claims.FirstOrDefault(c => c.Type == SanteDBClaimTypes.LocalOnly) != null)
                {
                    return false;
                }
            }

            return IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AuthenticationService);
        }

        public void SynchronizeLocalIdentity(IClaimsPrincipal remoteIdentity, string password)
        {
            if (null == remoteIdentity)
            {
                throw new ArgumentNullException(nameof(remoteIdentity), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (null == _LocalIdentityProvider) //If there is no local identity provider, then we don't need to do anything.
            {
                return;
            }

            var useridentity = remoteIdentity.Identity as IClaimsIdentity; //Coming from OAuth, there will always be one that represents the user.

            var policiestosync = new List<string>();
            var rolestosync = new List<string>();
            string userid = null;

            policiestosync.AddRange(remoteIdentity.Claims.Where(c => c.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(c => c.Value));
            rolestosync.AddRange(remoteIdentity.Claims.Where(c => c.Type == SanteDBClaimTypes.DefaultRoleClaimType).Select(c => c.Value));

            if (null != useridentity)
            {
                policiestosync.AddRange(useridentity.Claims.Where(c => c.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(c => c.Value));
                rolestosync.AddRange(useridentity.Claims.Where(c => c.Type == SanteDBClaimTypes.DefaultRoleClaimType).Select(c => c.Value));

                userid = useridentity.GetFirstClaimValue(SanteDBClaimTypes.SanteDBUserIdentifierClaim, SanteDBClaimTypes.SecurityId);
            }

            var localuser = _LocalIdentityProvider.LocalProvider.GetIdentity(useridentity.Name);

            if (null == localuser)
            {
                localuser = _LocalIdentityProvider.LocalProvider.CreateIdentity(useridentity.Name, password, AuthenticationContext.SystemPrincipal);
            }
            else
            {
                _LocalIdentityProvider.LocalProvider.ChangePassword(useridentity.Name, password, remoteIdentity, force: true);
            }

            if (_CanSyncPolicies)
            {
                foreach (var policyoid in policiestosync)
                {
                    var localpolicy = _LocalPolicyInformationService.LocalProvider.GetPolicy(policyoid);

                    if (null == localpolicy)
                    {
                        var remotepolicy = _RemotePolicyInformationService.UpstreamProvider.GetPolicy(policyoid);
                        _LocalPolicyInformationService.LocalProvider.CreatePolicy(remotepolicy, AuthenticationContext.SystemPrincipal);
                    }
                }
            }

            if (_CanSyncRoles)
            {
                var localroles = _LocalRoleProviderService.LocalProvider.GetAllRoles();
                foreach (var rolename in rolestosync)
                {
                    if (!localroles.Contains(rolename))
                    {
                        _LocalRoleProviderService.LocalProvider.CreateRole(rolename, AuthenticationContext.SystemPrincipal);
                    }

                    var role = new SecurityRole { Name = rolename }; //TODO: Current implementations will not allow this to work locally.

                    var rolepolicies = _RemotePolicyInformationService.UpstreamProvider.GetPolicies(role);

                    foreach (var policy in rolepolicies)
                    {
                        if (null == _LocalPolicyInformationService.LocalProvider.GetPolicy(policy.Policy.Oid))
                        {
                            _LocalPolicyInformationService.LocalProvider.CreatePolicy(policy.Policy, AuthenticationContext.SystemPrincipal);
                        }

                        _LocalPolicyInformationService.LocalProvider.AddPolicies(role, policy.Rule, AuthenticationContext.SystemPrincipal, policy.Policy.Oid);
                    }
                }
            }
        }



        public void ChangeRemotePassword(string username, string newPassword)
        {
            using (var amiclient = CreateAmiServiceClient())
            {
                var remoteuser = _SecurityRepositoryService.GetUser(username);

                if (null == remoteuser?.Key)
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(SecurityUser) }));
                }

                remoteuser.Password = newPassword;

                try
                {
                    var result = amiclient.UpdateUser(remoteuser.Key.Value, new Core.Model.AMI.Auth.SecurityUserInfo(remoteuser) { PasswordOnly = true });

                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = remoteuser.Key.Value }), ex);
                }
            }
        }

        public void AddClaim(string userName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            if (_LocalIdentityProvider == null) // If this is online only configured - local not allowed
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }

            _LocalIdentityProvider?.LocalProvider.AddClaim(userName, claim, principal, expiry);
        }

        public IPrincipal Authenticate(string userName, string password)
            => Authenticate(userName, password, null);

        public IPrincipal Authenticate(string userName, string password, string tfaSecret)
        {
            var authenticatingargs = new AuthenticatingEventArgs(userName);
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

                if (ShouldDoRemoteAuthentication(userName))
                {
                    try
                    {
                        result = _OAuthClient.AuthenticateUser(userName, password, tfaSecret: tfaSecret);

                        SynchronizeLocalIdentity(result, password);

                        return result;
                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), ex);
                    }
                }
                else if (_LocalIdentityProvider == null)
                {
                    throw new UpstreamIntegrationException(String.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
                }

                if (null != tfaSecret)
                {
                    result = _LocalIdentityProvider.LocalProvider.Authenticate(userName, password, tfaSecret) as IClaimsPrincipal;
                }
                else
                {
                    result = _LocalIdentityProvider.LocalProvider.Authenticate(userName, password) as IClaimsPrincipal;
                }

                return result;
            }
            finally
            {
                Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, result, null != result));
            }
        }



        public void ChangePassword(string userName, string newPassword, IPrincipal principal, bool force)
        {
            _LocalIdentityProvider?.LocalProvider.ChangePassword(userName, newPassword, principal);
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

            var result = _LocalIdentityProvider.LocalProvider.CreateIdentity(userName, password, principal);

            _LocalIdentityProvider.LocalProvider.AddClaim(userName, new SanteDBClaim(SanteDBClaimTypes.LocalOnly, "true"), principal);

            result = _LocalIdentityProvider.LocalProvider.GetIdentity(userName);

            return result;
        }

        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.LocalProvider.DeleteIdentity(userName, principal);
        }

        public AuthenticationMethod GetAuthenticationMethods(string userName)
        {
            if (null == _LocalIdentityProvider)
            {
                return AuthenticationMethod.Online;
            }

            try
            {
                var claims = _LocalIdentityProvider.LocalProvider.GetClaims(userName);

                if (claims?.Any(c => c.Type == SanteDBClaimTypes.LocalOnly) == true)
                {
                    return AuthenticationMethod.Local;
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {

            }

            return AuthenticationMethod.Local | AuthenticationMethod.Online;
        }

        public IEnumerable<IClaim> GetClaims(string userName)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.LocalProvider.GetClaims(userName);
        }

        public IIdentity GetIdentity(string userName)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.LocalProvider.GetIdentity(userName);
        }

        public IIdentity GetIdentity(Guid sid)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.LocalProvider.GetIdentity(sid);
        }

        public Guid GetSid(string name)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            return _LocalIdentityProvider.LocalProvider.GetSid(name);
        }

        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal is OAuthClaimsPrincipal oacp)
            {
                if (oacp.CanRefresh)
                {
                    var result = _OAuthClient.Refresh(oacp.GetRefreshToken());

                    SynchronizeLocalIdentity(result, null);

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
                return _LocalIdentityProvider.LocalProvider.ReAuthenticate(principal);
            }
        }

        public void RemoveClaim(string userName, string claimType, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.LocalProvider.RemoveClaim(userName, claimType, principal);
        }

        public void SetLockout(string userName, bool lockout, IPrincipal principal)
        {
            if (_LocalIdentityProvider == null)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.LOCAL_SERVICE_NOT_SUPPORTED, typeof(IIdentityProviderService)));
            }
            _LocalIdentityProvider.LocalProvider.SetLockout(userName, lockout, principal);
        }
    }
}
