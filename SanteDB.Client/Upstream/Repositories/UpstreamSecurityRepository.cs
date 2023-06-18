/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you
 * may not use this file except in compliance with the License. You may
 * obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 *
 * User: fyfej
 * Date: 2023-5-19
 */
using SanteDB.Client.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Services;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// A security repository that uses the upstream services to perform its duties
    /// </summary>
    public class UpstreamSecurityRepository : UpstreamServiceBase, ISecurityRepositoryService
    {
        private readonly ILocalizationService m_localizationService;
        private readonly IAdhocCacheService m_adhocCache;
        private readonly TimeSpan TEMP_CACHE_TIMEOUT = new TimeSpan(0, 0, 30);

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamSecurityRepository(IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            ILocalizationService localizationService,
            IAdhocCacheService adhocCacheService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localizationService = localizationService;
            this.m_adhocCache = adhocCacheService;
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Security Repository";

        /// <inheritdoc/>
        public SecurityUser ChangePassword(Guid userId, string password)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(userId));
            }
            else if (String.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    return client.UpdateUser(userId, new Core.Model.AMI.Auth.SecurityUserInfo()
                    {
                        PasswordOnly = true,
                        Entity = new SecurityUser()
                        {
                            Password = password
                        }
                    })?.Entity;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public SecurityUser CreateUser(SecurityUser userInfo, string password)
        {
            if (userInfo == null)
            {
                throw new ArgumentNullException(nameof(userInfo));
            }
            else if (String.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    userInfo.Password = password;
                    var retVal = client.CreateUser(new Core.Model.AMI.Auth.SecurityUserInfo(userInfo)
                    {
                        Roles = userInfo.Roles.Select(o => o.Name).ToList()
                    });
                    retVal.Entity.Roles = retVal.Roles.Select(o => this.GetRole(o)).ToList();
                    return retVal.Entity;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<SecurityProvenance> FindProvenance(Expression<Func<SecurityProvenance, bool>> query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return new UpstreamQueryResultSet<SecurityProvenance, AmiCollection>(this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal), query);
        }

        /// <inheritdoc/>
        public SecurityApplication GetApplication(string applicationName)
        {
            if (String.IsNullOrEmpty(applicationName))
            {
                throw new ArgumentNullException(nameof(applicationName));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.app.{applicationName}", out SecurityApplication retVal) == true)
            {
                return retVal;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var retVal = client.GetApplications(o => o.Name == applicationName).CollectionItem.OfType<SecurityApplicationInfo>().FirstOrDefault()?.Entity;
                    this.m_adhocCache?.Add($"sec.app.{applicationName}", retVal, this.TEMP_CACHE_TIMEOUT); // just saves some server calls
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public SecurityApplication GetApplication(IIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            return this.GetApplication(identity.Name);
        }

        /// <inheritdoc/>
        public SecurityDevice GetDevice(string deviceName)
        {
            if (String.IsNullOrEmpty(deviceName))
            {
                throw new ArgumentNullException(nameof(deviceName));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.dev.{deviceName}", out SecurityDevice retVal) == true)
            {
                return retVal;
            }
            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var retVal = client.GetDevices(o => o.Name == deviceName).CollectionItem.OfType<SecurityDeviceInfo>().FirstOrDefault()?.Entity;
                    this.m_adhocCache?.Add($"sec.dev.{deviceName}", retVal);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public SecurityDevice GetDevice(IIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return this.GetDevice(identity.Name);
        }

        /// <inheritdoc/>
        public SecurityPolicy GetPolicy(string policyOid)
        {
            if (String.IsNullOrEmpty(policyOid))
            {
                throw new ArgumentNullException(nameof(policyOid));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.pol.{policyOid}", out SecurityPolicy retVal))
            {
                return retVal;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var retVal = new UpstreamQueryResultSet<SecurityPolicy, AmiCollection>(client.Client, o => o.Oid == policyOid).FirstOrDefault();
                    this.m_adhocCache?.Add($"sec.pol.{policyOid}", retVal, TEMP_CACHE_TIMEOUT);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public SecurityProvenance GetProvenance(Guid provenanceId)
        {
            if (provenanceId == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(provenanceId));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.prov.{provenanceId}", out SecurityProvenance provenance) == true)
            {
                return provenance;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var retVal = client.GetProvenance(provenanceId);
                    this.m_adhocCache?.Add($"sec.prov.{provenanceId}", retVal, TEMP_CACHE_TIMEOUT);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public Provider GetProviderEntity(IIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.pvd.{identity.Name}", out Provider retVal) == true)
            {
                return retVal;
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.HealthDataService, AuthenticationContext.Current.Principal))
                {
                    var retVal = new UpstreamQueryResultSet<Provider, Bundle>(client, o => o.Relationships
                        .Where(r => r.RelationshipTypeKey == EntityRelationshipTypeKeys.EquivalentEntity && r.ClassificationKey == RelationshipClassKeys.PlayedRoleLink)
                        .Any(r => (r.SourceEntity as UserEntity).SecurityUser.UserName.ToLowerInvariant() == identity.Name.ToLowerInvariant())).FirstOrDefault();
                    this.m_adhocCache?.Add($"sec.pvd.{identity.Name}", retVal, TEMP_CACHE_TIMEOUT);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public SecurityRole GetRole(string roleName)
        {
            if (String.IsNullOrEmpty(roleName))
            {
                throw new ArgumentNullException(nameof(roleName));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.rol.{roleName}", out SecurityRole retVal) == true)
            {
                return retVal;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var retVal = client.GetRoles(o => o.Name == roleName).CollectionItem.OfType<SecurityRoleInfo>().FirstOrDefault()?.Entity;
                    this.m_adhocCache?.Add($"sec.rol.{roleName}", retVal);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public IdentifiedData GetSecurityEntity(IPrincipal principal)
        {
            switch (principal.Identity)
            {
                case IDeviceIdentity idi:
                    return this.GetDevice(idi);
                case IApplicationIdentity iai:
                    return this.GetApplication(iai);
                case IIdentity ii:
                    return this.GetUser(ii);
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
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.usr.{userName}", out SecurityUser user) == true)
            {
                return user;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var retVal = client.GetUsers(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant()).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault()?.Entity;
                    this.m_adhocCache?.Add($"sec.usr.{userName}", retVal);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public SecurityUser GetUser(IIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return this.GetUser(identity.Name);
        }

        /// <inheritdoc/>
        public UserEntity GetUserEntity(IIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.ue.{identity.Name}", out UserEntity retVal) == true)
            {
                return retVal;
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.HealthDataService, AuthenticationContext.Current.Principal))
                {
                    var retVal = new UpstreamQueryResultSet<UserEntity, Bundle>(client, o => o.SecurityUser.UserName.ToLowerInvariant() == identity.Name.ToLowerInvariant())
                        .FirstOrDefault();
                    this.m_adhocCache?.Add($"sec.ue.{identity.Name}", retVal, TEMP_CACHE_TIMEOUT);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
            }
        }

        /// <inheritdoc/>
        public void LockApplication(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    client.Lock<SecurityApplicationInfo>($"{typeof(SecurityApplication).GetSerializationName()}/{key}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public void LockDevice(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    client.Lock<SecurityDeviceInfo>($"{typeof(SecurityDevice).GetSerializationName()}/{key}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public void LockUser(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    client.Lock<SecurityUserInfo>($"{typeof(SecurityUser).GetSerializationName()}/{key}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public String ResolveName(Guid sid)
        {
            if (sid == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(sid));
            }
            else if (this.m_adhocCache != null && this.m_adhocCache.TryGet($"sec.sid.{sid}", out String retVal))
            {
                return retVal;
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    ISecurityEntityInfo securityObject = client.Get<SecurityUserInfo>($"{typeof(SecurityUser).GetSerializationName()}/{sid}") ??
                        (ISecurityEntityInfo)client.Get<SecurityDeviceInfo>($"{typeof(SecurityDevice).GetSerializationName()}/{sid}") ??
                        client.Get<SecurityApplicationInfo>($"{typeof(SecurityApplication).GetSerializationName()}/{sid}");

                    String retVal = null;
                    switch (securityObject)
                    {
                        case SecurityUserInfo sui:
                            retVal = sui.Entity.UserName;
                            break;
                        case SecurityDeviceInfo sdi:
                            retVal = sdi.Entity.Name;
                            break;
                        case SecurityApplicationInfo sai:
                            retVal = sai.Entity.Name;
                            break;
                        case SecurityRoleInfo sri:
                            retVal = sri.Entity.Name;
                            break;
                    }

                    this.m_adhocCache?.Add($"sec.sid.{sid}", retVal);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = "resolve" }), e);

            }
        }

        /// <inheritdoc/>
        public void UnlockApplication(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    client.Unlock<SecurityApplicationInfo>($"{typeof(SecurityApplication).GetSerializationName()}/{key}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public void UnlockDevice(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    client.Unlock<SecurityDeviceInfo>($"{typeof(SecurityDevice).GetSerializationName()}/{key}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public void UnlockUser(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    client.Unlock<SecurityUserInfo>($"{typeof(SecurityUser).GetSerializationName()}/{key}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }
    }
}
