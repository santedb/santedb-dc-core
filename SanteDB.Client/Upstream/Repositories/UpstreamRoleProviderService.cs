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
 * Date: 2023-3-10
 */
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Repositories
{
    /// <summary>
    /// A <see cref="IRoleProviderService"/> which manages upstream roles
    /// </summary>
    [PreferredService(typeof(IRoleProviderService))]
    public class UpstreamRoleProviderService : UpstreamServiceBase, IRoleProviderService
    {
        readonly ILocalizationService _LocalizationService;
        readonly Tracer _Tracer;

        /// <summary>
        /// DI ctor
        /// </summary>
        public UpstreamRoleProviderService(ILocalizationService localizationService, 
            IRestClientFactory restClientFactory, 
            IUpstreamManagementService upstreamManagementService, 
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService)
            : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this._Tracer = new Tracer(nameof(UpstreamRoleProviderService));
            this._LocalizationService = localizationService;
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Role Provider Service";

        /// <inheritdoc/>
        public void AddUsersToRoles(string[] users, string[] roles, IPrincipal principal)
        {
            if (null == users)
            {
                throw new ArgumentNullException(nameof(users));
            }
            else  if (null == roles)
            {
                throw new ArgumentNullException(nameof(roles));
            }
            else if (null == principal)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            if (!IsUpstreamConfigured)
            {
                this._Tracer.TraceWarning("Upstream is not configured; skipping.");
                return;
            }

            try
            {
                using (var client = CreateAmiServiceClient(principal))
                {
                    foreach (var role in roles)
                    {
                        client.AddUsersToRole(role, users);
                    }
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                this._Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(this._LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        /// <inheritdoc/>
        public void CreateRole(string roleName, IPrincipal principal)
        {
            if (string.IsNullOrEmpty(roleName))
            {
                throw new ArgumentNullException(nameof(roleName), this._LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (null == principal)
            {
                throw new ArgumentNullException(nameof(principal), this._LocalizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
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
                this._Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(this._LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        /// <inheritdoc/>
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
                    var amirole = amiclient.GetUsers(u => u.Roles.Any(r => r.Name == role))?.CollectionItem?.OfType<SecurityUserInfo>();
                    return amirole.Select(o=>o.Entity.UserName).ToArray();
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                this._Tracer.TraceError("Error getting roles: {0}", ex);
                throw new UpstreamIntegrationException(this._LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        /// <inheritdoc/>
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
                this._Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(this._LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
                    return amiclient.IsUserInRole(userName, roleName);
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error updating roles: {0}", ex);
                throw new UpstreamIntegrationException(_LocalizationService.GetString(ErrorMessageStrings.SEC_ROL_GEN), ex);
            }
        }

        /// <inheritdoc/>
        public void RemoveUsersFromRoles(string[] users, string[] roles, IPrincipal principal)
        {
            if (null == users)
            {
                throw new ArgumentNullException(nameof(users));
            }
            else if (null == roles)
            {
                throw new ArgumentNullException(nameof(roles));
            }
            else if (null == principal)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            try
            {
                using (var client = CreateAmiServiceClient(principal))
                {
                    foreach (var role in roles)
                    {
                        client.RemoveUsersFromRole(role, users);
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
