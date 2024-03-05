using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Security
{
    /// <summary>
    /// Upstream implementation of the certificate association manager
    /// </summary>
    public class UpstreamCertificateAssociationManager : UpstreamServiceBase, ICertificateIdentityProvider, IDataSigningCertificateManagerService
    {
        private readonly ILocalizationService m_localizationService;
        private readonly ISecurityRepositoryService m_securityRepositoryService;

        /// <summary>
        /// Upstream certificate association manager
        /// </summary>
        public UpstreamCertificateAssociationManager(IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            ISecurityRepositoryService securityRepositoryService,
            ILocalizationService localizationService,
            IUpstreamIntegrationService upstreamIntegrationService = null) :
            base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localizationService = localizationService;
            this.m_securityRepositoryService = securityRepositoryService;
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Certificate Mapping Provider";

#pragma warning disable CS0067
        /// <inheritdoc/>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        /// <inheritdoc/>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;
#pragma warning restore CS0067

        /// <inheritdoc/>
        public void AddIdentityMap(IIdentity identityToBeMapped, X509Certificate2 authenticationCertificate, IPrincipal authenticatedPrincipal)
            => this.AddCertificate(identityToBeMapped, authenticationCertificate, "auth_cert", authenticatedPrincipal);

        /// <inheritdoc/>
        public void AddSigningCertificate(IIdentity identity, X509Certificate2 x509Certificate, IPrincipal principal)
            => this.AddCertificate(identity, x509Certificate, "dsig_cert", principal);

        /// <inheritdoc/>
        public IPrincipal Authenticate(X509Certificate2 authenticationCertificate)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IIdentity GetCertificateIdentity(X509Certificate2 authenticationCertificate)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IEnumerable<X509Certificate2> GetIdentityCertificates(IIdentity identityOfCertificte)
            => this.GetCertificates(identityOfCertificte, "auth_cert");

        /// <inheritdoc/>
        public IEnumerable<X509Certificate2> GetSigningCertificates(IIdentity identity)
            => this.GetCertificates(identity, "dsig_cert");

        /// <inheritdoc/>
        public bool RemoveIdentityMap(IIdentity identityToBeUnMapped, X509Certificate2 authenticationCertificate, IPrincipal authenticatedPrincipal)
            => this.RemoveCertificate(identityToBeUnMapped, authenticationCertificate, "auth_cert", authenticatedPrincipal);

        /// <inheritdoc/>
        public void RemoveSigningCertificate(IIdentity identity, X509Certificate2 x509Certificate, IPrincipal principal)
            => this.RemoveCertificate(identity, x509Certificate, "dsig_cert", principal);

        /// <inheritdoc/>
        public bool TryGetSigningCertificateByHash(byte[] x509hash, out X509Certificate2 certificate)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public bool TryGetSigningCertificateByThumbprint(string x509Thumbprint, out X509Certificate2 certificate)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Add certificate to identity
        /// </summary>
        private void AddCertificate(IIdentity identity, X509Certificate2 certificate, String pathName, IPrincipal principal)
        {
            try
            {
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    var sid = this.m_securityRepositoryService.GetSecurityEntity(new GenericPrincipal(identity, new string[0]));
                    client.Post<X509Certificate2Info, X509Certificate2Info>($"{sid.GetType().GetSerializationName()}/{sid.Key}/{pathName}", new X509Certificate2Info(certificate));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(X509Certificate2) }), e);
            }
        }

        /// <summary>
        /// Get certiifcates for particular purpose
        /// </summary>
        private IEnumerable<X509Certificate2> GetCertificates(IIdentity identity, String pathName)
        {
            try
            {
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    var sid = this.m_securityRepositoryService.GetSecurityEntity(new GenericPrincipal(identity, new string[0]));
                    var results = client.Get<AmiCollection>($"{sid.GetType().GetSerializationName()}/{sid.Key}/{pathName}");
                    return results.CollectionItem.OfType<X509Certificate2Info>().Select(o => new X509Certificate2(o.PublicKey));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(X509Certificate2) }), e);
            }
        }

        /// <summary>
        /// Remove certificate for specified purpose from identity
        /// </summary>
        private bool RemoveCertificate(IIdentity identity, X509Certificate2 certificate, string pathName, IPrincipal principal)
        {
            try
            {
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    var sid = this.m_securityRepositoryService.GetSecurityEntity(new GenericPrincipal(identity, new string[0]));
                    client.Delete<X509Certificate2Info>($"{sid.GetType().GetSerializationName()}/{sid.Key}/{pathName}/{certificate.Thumbprint}");
                    return true;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(X509Certificate2) }), e);
            }
        }
    }
}
