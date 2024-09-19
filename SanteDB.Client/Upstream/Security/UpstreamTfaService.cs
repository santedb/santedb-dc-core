/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// A TFA service which communicates with the upstream
    /// </summary>
    public class UpstreamTfaService : UpstreamServiceBase, ITfaService
    {

        /// <summary>
        /// Upstream TFA mechanism
        /// </summary>
        private class UpstreamTfaMechanism : ITfaMechanism
        {

            // Mechanism
            private readonly TfaMechanismInfo m_mechanismInfo;

            /// <summary>
            /// Constructor
            /// </summary>
            public UpstreamTfaMechanism(TfaMechanismInfo tfaMechanismInfo)
            {
                this.m_mechanismInfo = tfaMechanismInfo;
            }

            /// <inheritdoc/>
            public SanteDBHostType[] HostTypes => new SanteDBHostType[] {
                SanteDBHostType.Client, SanteDBHostType.Debugger, SanteDBHostType.Gateway, SanteDBHostType.Other, SanteDBHostType.Test
            };

            /// <inheritdoc/>
            public Guid Id => this.m_mechanismInfo.Id;

            /// <inheritdoc/>
            public string Name => this.m_mechanismInfo.Name;

            /// <inheritdoc/>
            public TfaMechanismClassification Classification => this.m_mechanismInfo.Classification;

            /// <inheritdoc/>
            public string SetupHelpText => this.m_mechanismInfo.HelpText;

            /// <inheritdoc/>
            public string BeginSetup(IIdentity user)
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public bool EndSetup(IIdentity user, string verificationCode)
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public string Send(IIdentity user)
            {
                throw new NotSupportedException();
            }

            /// <inheritdoc/>
            public bool Validate(IIdentity user, string secret)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// TFA mechanisms
        /// </summary>
        private ITfaMechanism[] m_mechanisms = null;
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// DI construcotr
        /// </summary>
        public UpstreamTfaService(IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            ILocalizationService localizationService,
            IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Get the TFA mechansisms from the upstream
        /// </summary>
        public IEnumerable<ITfaMechanism> Mechanisms
        {
            get
            {
                if (this.m_mechanisms == null &&
                    this.IsUpstreamConfigured &&
                    this.IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    using (var client = this.CreateAmiServiceClient())
                    {
                        this.m_mechanisms = client.GetTwoFactorMechanisms().CollectionItem.OfType<TfaMechanismInfo>().Select(o => new UpstreamTfaMechanism(o)).ToArray();
                    }
                }
                return this.m_mechanisms ?? new ITfaMechanism[0];

            }
        }


        /// <inheritdoc/>
        public string ServiceName => "Upstream TFA Service";

        /// <inheritdoc/>
        public string SendSecret(Guid mechanismId, IIdentity user)
        {
            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    var response = client.Client.Post<ParameterCollection, ParameterCollection>("/Tfa/$send", new ParameterCollection(
                        new Parameter("userName", user.Name),
                        new Parameter("mechanism", mechanismId)
                    ));
                    if(response.TryGet("challenge", out string challenge))
                    {
                        return challenge;
                    }
                }
                return String.Empty;
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { resource = "Tfa" }), e);
            }
        }

        /// <summary>
        /// Validate the secret
        /// </summary>
        public bool ValidateSecret(Guid mechanismId, IIdentity user, string secret)
        {
            throw new NotSupportedException();
        }
    }
}
