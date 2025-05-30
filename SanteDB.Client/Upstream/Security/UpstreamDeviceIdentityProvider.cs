﻿/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Constants;
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

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// Represents an identity provider that provides upstream device identities 
    /// </summary>
    /// <remarks>This is a partial implementation only for the resolution of identity objects</remarks>
    public class UpstreamDeviceIdentityProvider : UpstreamServiceBase, IDeviceIdentityProviderService
    {

        /// <summary>
        /// Upstream application identity used for GetIdentity calls
        /// </summary>
        private class UpstreamDeviceIdentity : SanteDBClaimsIdentity, IDeviceIdentity
        {
            /// <summary>
            /// Create a new 
            /// </summary>
            public UpstreamDeviceIdentity(SecurityDevice device)
                : base(device.Name, false, "NONE")
            {
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.Actor, ActorTypeKeys.Device.ToString()));
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.NameIdentifier, device.Key.ToString()));
                this.AddClaim(new SanteDBClaim(SanteDBClaimTypes.SecurityId, device.Key.ToString()));
            }
        }


        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// DI ctor
        /// </summary>
        public UpstreamDeviceIdentityProvider(
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            ILocalizationService localizationService,
            IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "Upstream Device Identity Provider";

#pragma warning disable CS0067
        /// <inheritdoc/>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        /// <inheritdoc/>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;
#pragma warning restore CS0067

        /// <summary>
        /// Get upstream device based on the name
        /// </summary>
        private SecurityDeviceInfo GetUpstreamDeviceData(Expression<Func<SecurityDevice, bool>> query, IPrincipal principal)
        {
            using (var amiclient = base.CreateAmiServiceClient())
            {
                try
                {
                    return amiclient.GetDevices(query).CollectionItem.OfType<SecurityDeviceInfo>().FirstOrDefault();
                }
                catch (Exception e)
                {
                    throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = query }), e);
                }
            }
        }

        /// <inheritdoc/>
        public void AddClaim(string deviceName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            throw new NotSupportedException(ErrorMessageStrings.UPSTREAM_CLAIMS_READONLY);
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string deviceName, string deviceSecret, AuthenticationMethod authMethod = AuthenticationMethod.Any)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void ChangeSecret(string deviceName, string deviceSecret, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IDeviceIdentity CreateIdentity(string deviceName, string secret, IPrincipal principal, Guid? withSid = null)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IEnumerable<IClaim> GetClaims(string deviceName)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IDeviceIdentity GetIdentity(string deviceName)
        {
            var remoteData = this.GetUpstreamDeviceData(o => o.Name.ToLowerInvariant() == deviceName.ToLowerInvariant(), AuthenticationContext.Current.Principal);
            if (remoteData != null)
            {
                return new UpstreamDeviceIdentity(remoteData.Entity);
            }
            return null;
        }

        /// <inheritdoc/>
        public IDeviceIdentity GetIdentity(Guid sid)
        {
            var remoteData = this.GetUpstreamDeviceData(o => o.Key == sid, AuthenticationContext.Current.Principal);
            if (remoteData != null)
            {
                return new UpstreamDeviceIdentity(remoteData.Entity);
            }
            return null;
        }

        /// <inheritdoc/>
        public Guid GetSid(string deviceName)
         => this.GetUpstreamDeviceData(o => o.Name.ToLowerInvariant() == deviceName.ToLowerInvariant(), AuthenticationContext.Current.Principal)?.Key ?? Guid.Empty;

        /// <inheritdoc/>
        public void RemoveClaim(string deviceName, string claimType, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void SetLockout(string deviceName, bool lockoutState, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

    }
}
