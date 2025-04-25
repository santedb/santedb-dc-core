/*
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
using SanteDB.Client.Upstream.Management;
using SanteDB.Client.Upstream.Matching;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Client.Upstream.Security;
using SanteDB.Core.Configuration;
using SanteDB.Core.Data;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;

namespace SanteDB.Client.Configuration
{
    /// <summary>
    /// Online
    /// </summary>
    public class OnlineIntegrationPattern : IUpstreamIntegrationPattern
    {

        /// <summary>
        /// Integration pattern anme
        /// </summary>
        public const string INTEGRATION_PATTERN_NAME = "online";

        /// <inheritdoc/>
        public string Name => INTEGRATION_PATTERN_NAME;

        /// <inheritdoc/>
        public IEnumerable<Type> GetServices() =>
                    new Type[] {
                        typeof(DefaultPolicyDecisionService),
                        typeof(UpstreamJobManager),
                        typeof(UpstreamForeignDataManagement),
                        typeof(UpstreamRepositoryFactory),
                        typeof(UpstreamDataTemplateManagementService),
                        typeof(UpstreamTfaService),
                        typeof(UpstreamIdentityProvider),
                        typeof(UpstreamDeviceIdentityProvider),
                        typeof(UpstreamCertificateAssociationManager),
                        typeof(UpstreamApplicationIdentityProvider),
                        typeof(UpstreamPolicyInformationService),
                        typeof(UpstreamRoleProviderService),
                        typeof(UpstreamMatchConfigurationService),
                        typeof(UpstreamSecurityRepository),
                        typeof(UpstreamSecurityChallengeProvider),
                        typeof(RepositoryEntitySource),
                        typeof(UpstreamResourceCheckoutService)
                    };

        /// <inheritdoc/>
        public void SetDefaults(SanteDBConfiguration configuration) { }
    }
}
