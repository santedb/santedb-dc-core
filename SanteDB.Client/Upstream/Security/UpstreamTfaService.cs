using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
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
using System.Text;

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
            public Guid Id => this.m_mechanismInfo.Id;

            /// <inheritdoc/>
            public string Name => this.m_mechanismInfo.Name;

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
                    client.Client.Post<ParameterCollection, object>("/Tfa/$send", new ParameterCollection(
                        new Parameter("userName", user.Name),
                        new Parameter("mechanism", mechanismId)
                    ));
                }
                return String.Empty;
            }
            catch(Exception e)
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
