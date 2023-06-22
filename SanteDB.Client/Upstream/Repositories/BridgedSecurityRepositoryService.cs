using SanteDB.Client.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// Represents a security repository service that will use local or upstream data based on the availibity of the services for upstream
    /// </summary>
    [PreferredService(typeof(ISecurityRepositoryService))]
    public class BridgedSecurityRepositoryService : UpstreamServiceBase, ISecurityRepositoryService
    {
        private readonly IIdentityProviderService m_localIdentityProvider;
        private readonly ISecurityRepositoryService m_localSecurityRepository;
        private readonly ISecurityRepositoryService m_upstreamSecurityRepository;
        private readonly IApplicationIdentityProviderService m_localApplicationProvider;
        private readonly IDeviceIdentityProviderService m_localDeviceProvider;
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public BridgedSecurityRepositoryService(IRestClientFactory restClientFactory, 
            IUpstreamManagementService upstreamManagementService, 
            ILocalServiceProvider<ISecurityRepositoryService> localSecurityRepository,
            IUpstreamServiceProvider<ISecurityRepositoryService> upstreamSecurityRepository,
            ILocalServiceProvider<IIdentityProviderService> localIdentityProvider,
            ILocalServiceProvider<IApplicationIdentityProviderService> localApplicationProvider,
            ILocalServiceProvider<IDeviceIdentityProviderService> localDeviceProvider,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            ILocalizationService localizationService) 
            : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider)
        {
            this.m_localIdentityProvider = localIdentityProvider.LocalProvider;
            this.m_localSecurityRepository = localSecurityRepository.LocalProvider;
            this.m_upstreamSecurityRepository = upstreamSecurityRepository.UpstreamProvider;
            this.m_localApplicationProvider = localApplicationProvider.LocalProvider;
            this.m_localDeviceProvider = localDeviceProvider.LocalProvider;
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Returns true if <paramref name="userName"/> is a local only identity
        /// </summary>
        private bool IsLocalUser(string userName)
        {
            return this.m_localIdentityProvider.GetClaims(userName)?.Any(c => c.Type == SanteDBClaimTypes.LocalOnly) == true;
        }

        /// <summary>
        /// Returns true if <paramref name="applicationName"/> is a local only identity
        /// </summary>
        private bool IsLocalApplication(string applicationName)
        {
            return this.m_localApplicationProvider.GetClaims(applicationName)?.Any(c => c.Type == SanteDBClaimTypes.LocalOnly) == true;
        }

        /// <summary>
        /// Returns true if <paramref name="deviceName"/> is a local only identity
        /// </summary>
        private bool IsLocalDevice(string deviceName)
        {
            return this.m_localDeviceProvider.GetClaims(deviceName)?.Any(c => c.Type == SanteDBClaimTypes.LocalOnly) == true;
        }

        /// <inheritdoc/>
        public string ServiceName => "Bridged Security Repository";

        /// <inheritdoc/>
        public SecurityUser ChangePassword(Guid userId, string password)
        {
            // Always use the local service for the changing of passwords and fall back to the higher level service
            var identity = this.m_localIdentityProvider.GetIdentity(userId);
            if(identity == null) // We don't have any right to change this password as the user has no business being on this device
            {
                throw new KeyNotFoundException(userId.ToString());
            }

            // Change password of upstream?
            if(this.IsLocalUser(identity.Name))
            {
                return this.m_localSecurityRepository.ChangePassword(userId, password);
            }
            else if(!this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.CONNECTION_REQUIRED));
            }
            else
            {
                this.m_upstreamSecurityRepository.ChangePassword(userId, password);
                return this.m_localSecurityRepository.ChangePassword(userId, password);
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<SecurityProvenance> FindProvenance(Expression<Func<SecurityProvenance, bool>> query)
        {
            var localProvenance = this.m_localSecurityRepository.FindProvenance(query);
            if(this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                localProvenance = localProvenance.Union(this.m_upstreamSecurityRepository.FindProvenance(query));
            }
            return localProvenance;
        }

        /// <inheritdoc/>
        public SecurityApplication GetApplication(string applicationName)
        {
            if(this.IsLocalApplication(applicationName))
            {
                return this.m_localSecurityRepository.GetApplication(applicationName);
            }
            else 
            {
                return this.m_upstreamSecurityRepository.GetApplication(applicationName);
            }
        }

        /// <inheritdoc/>
        public SecurityApplication GetApplication(IIdentity identity)
        {
            if(this.IsLocalApplication(identity.Name))
            {
                return this.m_localSecurityRepository.GetApplication(identity);
            }
            else
            {
                return this.m_upstreamSecurityRepository.GetApplication(identity);
            }
        }

        /// <inheritdoc/>
        public SecurityDevice GetDevice(string deviceName)
        {
            if (this.IsLocalDevice(deviceName))
            {
                return this.m_localSecurityRepository.GetDevice(deviceName);
            }
            else
            {
                return this.m_upstreamSecurityRepository.GetDevice(deviceName);
            }
        }

        /// <inheritdoc/>
        public SecurityDevice GetDevice(IIdentity identity)
        {
            if (this.IsLocalDevice(identity.Name))
            {
                return this.m_localSecurityRepository.GetDevice(identity);
            }
            else
            {
                return this.m_upstreamSecurityRepository.GetDevice(identity);
            }
        }

        /// <inheritdoc/>
        public SecurityPolicy GetPolicy(string policyOid)
        {
            if(this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                return this.m_upstreamSecurityRepository.GetPolicy(policyOid);
            }
            else
            {
                return this.m_localSecurityRepository.GetPolicy(policyOid);
            }
        }

        /// <inheritdoc/>
        public SecurityProvenance GetProvenance(Guid provenanceId)
        {
            return this.m_localSecurityRepository.GetProvenance(provenanceId) ??
                this.m_upstreamSecurityRepository.GetProvenance(provenanceId);
        }

        /// <inheritdoc/>
        public Provider GetProviderEntity(IIdentity identity)
        {
            if(this.IsLocalUser(identity.Name))
            {
                return this.m_localSecurityRepository.GetProviderEntity(identity);
            }
            else if(this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.HealthDataService))
            {
                return this.m_upstreamSecurityRepository.GetProviderEntity(identity);
            }
            else
            {
                return this.m_localSecurityRepository.GetProviderEntity(identity);
            }
        }

        /// <inheritdoc/>
        public SecurityRole GetRole(string roleName)
        {
            var role = this.m_localSecurityRepository.GetRole(roleName);
            if(role == null && this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                role = this.m_upstreamSecurityRepository.GetRole(roleName);
            }
            return role;
        }

        /// <inheritdoc/>
        public IdentifiedData GetSecurityEntity(IPrincipal principal)
        {
            var upstreamAvailable = this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService);
            switch (principal.Identity)
            {
                case IDeviceIdentity idi:
                    if(this.IsLocalDevice(idi.Name) || !upstreamAvailable)
                    {
                        return this.m_localSecurityRepository.GetDevice(idi);
                    }
                    else 
                    {
                        return this.m_upstreamSecurityRepository.GetDevice(idi);
                    }
                case IApplicationIdentity iai:
                    if (this.IsLocalApplication(iai.Name) || !upstreamAvailable)
                    {
                        return this.m_localSecurityRepository.GetApplication(iai);
                    }
                    else
                    {
                        return this.m_upstreamSecurityRepository.GetApplication(iai);
                    }
                case IIdentity ii:
                    if (this.IsLocalUser(ii.Name) || !upstreamAvailable)
                    {
                        return this.m_localSecurityRepository.GetUser(ii);
                    }
                    else
                    {
                        return this.m_upstreamSecurityRepository.GetUser(ii);
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(principal));
            }
        }

        /// <inheritdoc/>
        public Guid GetSid(IIdentity identity)
        {
            switch (identity)
            {
                case IDeviceIdentity idi:
                    return this.GetDevice(idi)?.Key ?? Guid.Empty;
                case IApplicationIdentity iai:
                    return this.GetApplication(iai)?.Key ?? Guid.Empty;
                case IIdentity ii:
                    return this.GetUser(ii)?.Key ?? Guid.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(identity));
            }
        }

        /// <inheritdoc/>
        public SecurityUser GetUser(string userName)
        {
            if(this.IsLocalUser(userName) || !this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                return this.m_localSecurityRepository.GetUser(userName);
            }
            else
            {
                return this.m_upstreamSecurityRepository.GetUser(userName);
            }
        }

        /// <inheritdoc/>
        public SecurityUser GetUser(IIdentity identity) => this.GetUser(identity.Name);

        /// <inheritdoc/>
        public UserEntity GetUserEntity(IIdentity identity)
        {
            if(this.IsLocalUser(identity.Name) || !this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.HealthDataService))
            {
                return this.m_localSecurityRepository.GetUserEntity(identity);
            }
            else
            {
                return this.m_upstreamSecurityRepository.GetUserEntity(identity);
            }
        }

        /// <inheritdoc/>
        public void LockApplication(Guid key)
        {
            // Locks can only be applied on local objects
            this.m_localSecurityRepository.LockApplication(key);
        }

        /// <inheritdoc/>
        public void LockDevice(Guid key)
        {
            this.m_localSecurityRepository.LockDevice(key);

        }

        /// <inheritdoc/>
        public void LockUser(Guid userId)
        {
            this.m_localSecurityRepository.LockUser(userId);

        }

        /// <inheritdoc/>
        public string ResolveName(Guid sid)
        {
            var resolved = this.m_localSecurityRepository.ResolveName(sid);
            if(resolved == null && this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                resolved = this.m_upstreamSecurityRepository.ResolveName(sid);
            }
            return resolved;
        }

        /// <inheritdoc/>
        public void UnlockApplication(Guid key)
        {
            this.m_localSecurityRepository.UnlockApplication(key);
        }

        /// <inheritdoc/>
        public void UnlockDevice(Guid key)
        {
            this.m_localSecurityRepository.UnlockDevice(key);
        }

        /// <inheritdoc/>
        public void UnlockUser(Guid userId)
        {
            this.m_localSecurityRepository.UnlockUser(userId);
        }
    }
}
