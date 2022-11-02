using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Repositories
{
    [PreferredService(typeof(IRoleProviderService))]
    public class UpstreamRoleProviderService : UpstreamServiceBase, IRoleProviderService
    {
        readonly ILocalizationService _LocalizationService;
        readonly Tracer _Tracer;

        public UpstreamRoleProviderService(ILocalizationService localizationService, 
            IRestClientFactory restClientFactory, 
            IUpstreamManagementService upstreamManagementService, 
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService)
            : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            _Tracer = new Tracer(nameof(UpstreamRoleProviderService));
            _LocalizationService = localizationService;
        }

        public string ServiceName => "Upstream Role Provider Service";

        public void AddUsersToRoles(string[] users, string[] roles, IPrincipal principal)
        {
            if (null == users)
            {
                throw new ArgumentNullException(nameof(users), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (null == roles)
            {
                throw new ArgumentNullException(nameof(roles), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (null == principal)
            {
                throw new ArgumentNullException(nameof(principal), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (!IsUpstreamConfigured)
            {
                _Tracer.TraceWarning("Upstream is not configured; skipping.");
                return;
            }

            try
            {
                using (var client = CreateAmiServiceClient(principal))
                {
                    var amiroles = client.GetRoles(r => roles.Contains(r.Name)).CollectionItem.OfType<SecurityRoleInfo>().ToList();

                    foreach (var role in amiroles)
                    {
                        role.Users.AddRange(users);

                        client.UpdateRole(role.Entity.Key.Value, role);
                    }
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        public void CreateRole(string roleName, IPrincipal principal)
        {
            if (string.IsNullOrEmpty(roleName))
            {
                throw new ArgumentNullException(nameof(roleName), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (null == principal)
            {
                throw new ArgumentNullException(nameof(principal), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            try
            {
                using (var amiclient = CreateAmiServiceClient(principal))
                {
                    amiclient.CreateRole(new SecurityRoleInfo
                    {
                        Entity = new Core.Model.Security.SecurityRole
                        {
                            Name = roleName
                        }
                    });
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        public string[] FindUsersInRole(string role)
        {
            if (string.IsNullOrEmpty(role))
            {
                throw new ArgumentNullException(nameof(role), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            try
            {
                using (var amiclient = CreateAmiServiceClient())
                {
                    var amirole = amiclient.GetRoles(r => r.Name == role)?.CollectionItem?.OfType<SecurityRoleInfo>()?.FirstOrDefault();

                    return amirole?.Users?.ToArray();
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        public string[] GetAllRoles()
        {
            try
            {
                using (var amiclient = CreateAmiServiceClient())
                {
                    return amiclient.GetRoles(r => true)?.CollectionItem?.OfType<SecurityRoleInfo>()?.Select(sri => sri.Entity.Name)?.ToArray();
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        public string[] GetAllRoles(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            try
            {
                using (var amiclient = CreateAmiServiceClient())
                {
                    var user = amiclient.GetUsers(u => u.UserName == userName)?.CollectionItem?.OfType<SecurityUserInfo>()?.FirstOrDefault();

                    return user?.Roles?.ToArray();
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        public bool IsUserInRole(string userName, string roleName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (string.IsNullOrEmpty(roleName))
            {
                throw new ArgumentNullException(nameof(roleName), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            try
            {
                using (var amiclient = CreateAmiServiceClient())
                {
                    var role = amiclient.GetRoles(r => r.Name == roleName)?.CollectionItem?.OfType<SecurityRoleInfo>()?.FirstOrDefault();

                    return role?.Users?.Contains(userName) ?? false;
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        public void RemoveUsersFromRoles(string[] users, string[] roles, IPrincipal principal)
        {
            if (null == users)
            {
                throw new ArgumentNullException(nameof(users), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (null == roles)
            {
                throw new ArgumentNullException(nameof(roles), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (null == principal)
            {
                throw new ArgumentNullException(nameof(principal), _LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            try
            {
                using (var client = CreateAmiServiceClient(principal))
                {
                    var amiroles = client.GetRoles(r => roles.Contains(r.Name)).CollectionItem.OfType<SecurityRoleInfo>().ToList();

                    foreach (var role in amiroles)
                    {
                        role.Users.RemoveAll(u => users.Contains(u));

                        client.UpdateRole(role.Entity.Key.Value, role);
                    }
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }
    }
}
