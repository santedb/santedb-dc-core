using RestSrvr;
using SanteDB;
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Http;
using SanteDB.Client.Services;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Client.Upstream.Security;
using SanteDB.Core;
using SanteDB.Core.Configuration.Http;
using SanteDB.Core.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Certs;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Messaging.HDSI.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// An upstream management service which manages the upstream
    /// </summary>
    [ServiceProvider("Upstream Management", Type = ServiceInstantiationType.Singleton)]
    public class DefaultUpstreamManagementService : IUpstreamManagementService
    {
        private readonly ILocalizationService m_localizationService;
        private readonly IPolicyEnforcementService m_policyEnforcementService;
        private readonly IServiceManager m_serviceManager;
        private readonly ICertificateGeneratorService m_certificateGenerator;
        private readonly IOperatingSystemInfoService m_operatingSystemInfo;
        private ConfiguredUpstreamRealmSettings m_upstreamSettings;
        private readonly IRestClientFactory m_restClientFactory;
        private readonly UpstreamConfigurationSection m_configuration;
        private readonly RestClientConfigurationSection m_restConfiguration;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(DefaultUpstreamManagementService));
        private readonly IGeographicLocationProvider m_geographicLocationService;
        private IAuditService m_auditService;
        private readonly SecurityConfigurationSection m_securityConfiguration;

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUpstreamManagementService(
            IRestClientFactory restClientFactory,
            IConfigurationManager configurationManager,
            ILocalizationService localizationService,
            IServiceManager serviceManager,
            IOperatingSystemInfoService operatingSystemInfoService, 
            IGeographicLocationProvider geographicLocationProvider = null,
            ICertificateGeneratorService certificateGenerator = null,
            IPolicyEnforcementService pepService = null
            )
        {
            this.m_restClientFactory = restClientFactory;
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>();
            this.m_restConfiguration = configurationManager.GetSection<RestClientConfigurationSection>();
            this.m_securityConfiguration = configurationManager.GetSection<SecurityConfigurationSection>();
            this.m_localizationService = localizationService;
            this.m_policyEnforcementService = pepService;
            this.m_serviceManager = serviceManager;
            this.m_certificateGenerator = certificateGenerator;
            this.m_operatingSystemInfo = operatingSystemInfoService;
            this.m_geographicLocationService = geographicLocationProvider;
            if (m_configuration?.Realm != null)
            {
                this.m_upstreamSettings = new ConfiguredUpstreamRealmSettings(m_configuration);
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "Default Upstream Management Service";

        /// <inheritdoc/>
        public event EventHandler<UpstreamRealmChangedEventArgs> RealmChanging;

        /// <inheritdoc/>
        public event EventHandler<UpstreamRealmChangedEventArgs> RealmChanged;

        /// <inheritdoc/>
        public bool IsConfigured() => m_upstreamSettings != null;

        /// <inheritdoc/>
        public IUpstreamRealmSettings GetSettings() => m_upstreamSettings;

        /// <inheritdoc/>
        public void Join(IUpstreamRealmSettings targetRealm, bool replaceExistingRegistration, out string welcomeMessage)
        {

            if (this.IsConfigured())
            {
                throw new InvalidOperationException(string.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(Join)));
            }
            else if (targetRealm == null)
            {
                throw new ArgumentNullException(nameof(targetRealm));
            }

            if (this.IsConfigured())
            {
                this.m_policyEnforcementService?.Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, AuthenticationContext.Current.Principal);
            }

            if(this.m_auditService == null)
            {
                this.m_auditService = ApplicationServiceContext.Current.GetService<IAuditService>();
            }

            this.m_tracer.TraceInfo("Will attempt to join domain {0}", targetRealm.Realm);

            var backupConfiguration = this.m_restConfiguration.Client.ToArray();
            this.m_restConfiguration.Client.Clear();

            // Get the service client section and create an AMI service client
            this.m_restConfiguration.Client.Add(new RestClientDescriptionConfiguration()
            {
                Accept = "application/xml",
                Name = ServiceEndpointType.AdministrationIntegrationService.ToString(),
                Binding = new RestClientBindingConfiguration()
                {
                    ContentTypeMapper = new DefaultContentTypeMapper(),
                    OptimizationMethod = Core.Http.Description.HttpCompressionAlgorithm.Gzip,
                    CompressRequests = true,
                    Security = new RestClientSecurityConfiguration()
                    {
                        AuthRealm = targetRealm.Realm.Host,
                        CredentialProvider = new UpstreamDeviceCredentialProvider(),
                        PreemptiveAuthentication = true
                    }
                },
                Endpoint = new List<RestClientEndpointConfiguration>()
                    {
                        new RestClientEndpointConfiguration($"{targetRealm.Realm}ami")
                    }
            });
            var amiRestClient = m_restClientFactory.GetRestClientFor(ServiceEndpointType.AdministrationIntegrationService);

            var audit = this.m_auditService.Audit()
                   .WithPrincipal()
                   .WithLocalSource()
                   .WithAction(Core.Model.Audit.ActionType.Execute)
                   .WithEventIdentifier(Core.Model.Audit.EventIdentifierType.ApplicationActivity)
                   .WithEventType(EventTypeCodes.SecurityConfigurationChanged)
                   .WithSystemObjects(Core.Model.Audit.AuditableObjectRole.SecurityResource, Core.Model.Audit.AuditableObjectLifecycle.Access, targetRealm.Realm)
                   .WithTimestamp()
                   .WithHttpInformation(RestOperationContext.Current.IncomingRequest);

            // Add the OAUTH service provider 
            try
            {
                var dnSettings = String.Empty;

                using (amiRestClient)
                {
                    using (var amiServiceClient = new AmiServiceClient(amiRestClient))
                    {
                        var realmOptions = amiServiceClient.Options(); // Get the options object from the server
                        dnSettings = realmOptions.Settings.Find(o => o.Key.Equals("dn", StringComparison.OrdinalIgnoreCase))?.Value;
                        welcomeMessage = realmOptions.Settings.Find(o => o.Key.Equals("welcome"))?.Value ?? this.m_localizationService.GetString(UserMessageStrings.JOIN_REALM_SUCCESS, new { realm = targetRealm.Realm.Host });
                        // Pull security configuration sections from the AMI - these are only disclosed when the AMI has our authentication as an administrator
                        this.m_securityConfiguration.PasswordRegex = realmOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.PasswordValidationDisclosureName)?.Value ??
                            this.m_securityConfiguration.PasswordRegex;
                        this.m_securityConfiguration.SetPolicy(Core.Configuration.SecurityPolicyIdentification.SessionLength, TimeSpan.Parse(realmOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.LocalSessionLengthDisclosureName)?.Value ?? "00:30:00"));
                        this.m_securityConfiguration.SetPolicy(Core.Configuration.SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts, Boolean.Parse(realmOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.LocalAccountAllowedDisclosureName)?.Value ?? "false"));

                        // Is the server compatible?
                        if (!replaceExistingRegistration &&
                            (!Version.TryParse(realmOptions.InterfaceVersion, out var ifVersion) ||
                            ifVersion.Major < this.GetType().Assembly.GetName().Version.Major))
                        {
                            throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_VERSION_MISMATCH, new { remote = realmOptions.InterfaceVersion }));
                        }
                        else if (realmOptions.Key == ApplicationServiceContext.Current.ActivityUuid)
                        {
                            throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_CANNOT_JOIN_YOURSELF));
                        }

                        this.m_restConfiguration.Client.Clear();
                        this.m_restConfiguration.Client.AddRange(GetRestClients(targetRealm, realmOptions));
                    }
                }

                // Invoke changing handler
                this.RealmChanging?.Invoke(this, new UpstreamRealmChangedEventArgs(targetRealm));

                // Generate the device secret or certificate
                var deviceCredential = this.m_configuration.Credentials.First(o => o.CredentialType == UpstreamCredentialType.Device);
                deviceCredential.CredentialName = targetRealm.LocalDeviceName;
                var secretBytes = new byte[64];
                System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(secretBytes);
                deviceCredential.CredentialSecret = secretBytes.HexEncode();

                using (var restClient = this.m_restClientFactory.GetRestClientFor(ServiceEndpointType.AdministrationIntegrationService))
                using (var amiClient = new AmiServiceClient(restClient))
                {
                    // Register the device
                    var existingDevice = amiClient.GetDevices(o => o.Name.ToLowerInvariant() == deviceCredential.CredentialName.ToLowerInvariant()).CollectionItem.OfType<SecurityDeviceInfo>().FirstOrDefault()?.Entity;
                    if (existingDevice != null && !replaceExistingRegistration)
                    {
                        throw new DuplicateNameException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_DEVICE_DUPLICATE, new { device = existingDevice.Name }));
                    }
                    else if (existingDevice != null)
                    {
                        existingDevice.DeviceSecret = deviceCredential.CredentialSecret;
                        existingDevice = amiClient.UpdateDevice(existingDevice.Key.Value, new SecurityDeviceInfo(existingDevice))?.Entity;
                        audit.WithSystemObjects(AuditableObjectRole.SecurityUser, AuditableObjectLifecycle.Amendment, existingDevice);

                    }
                    else
                    {
                        existingDevice = amiClient.CreateDevice(new SecurityDeviceInfo(new Core.Model.Security.SecurityDevice()
                        {
                            DeviceSecret = deviceCredential.CredentialSecret,
                            Name = deviceCredential.CredentialName
                        }))?.Entity;
                        audit.WithSystemObjects(AuditableObjectRole.SecurityUser, AuditableObjectLifecycle.Amendment, existingDevice);

                    }

                    var entity = this.CreateDeviceEntity(existingDevice);
                    audit.WithIdentifiedData(AuditableObjectLifecycle.Creation, entity);
                    var authEpConfiguration = this.m_restConfiguration.Client.Find(o => o.Name == ServiceEndpointType.AuthenticationService.ToString());
                    if (targetRealm.Realm.Scheme == "https" && 
                        authEpConfiguration.Binding.Security.Mode == Core.Http.Description.SecurityScheme.ClientCertificate)
                    {

                        var subjectName = $"CN={existingDevice.Key}, DC={targetRealm.LocalDeviceName}, DC={ targetRealm.Realm.Host}";
                        if(!String.IsNullOrEmpty(dnSettings))
                        {
                            subjectName += $", {dnSettings}";
                        }

                        deviceCredential.CertificateSecret = new Core.Security.Configuration.X509ConfigurationElement(StoreLocation.CurrentUser, StoreName.My, X509FindType.FindBySubjectDistinguishedName, subjectName);

                        // Is there already a certificate that has a private key?
                        var deviceCertificate = deviceCredential.CertificateSecret.Certificate;
                        if (deviceCertificate == null ||
                            !deviceCertificate.Verify()) // No certificate
                        {
                            if (this.m_certificateGenerator == null)
                            {
                                throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_CANNOT_GENERATE_CERTIFICATE));
                            }

                            var privateKeyPair = this.m_certificateGenerator.CreateKeyPair(2048);
                            var csr = this.m_certificateGenerator.CreateSigningRequest(privateKeyPair, new X500DistinguishedName(subjectName), X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement, new string[] { ExtendedKeyUsageOids.ClientAuthentication });
                            
                            var submissionResult = amiClient.SubmitCertificateSigningRequest(new Core.Model.AMI.Security.SubmissionRequest(csr, AuthenticationContext.Current.Principal));

                            if (submissionResult.Status == Core.Model.AMI.Security.SubmissionStatus.Issued &&
                                submissionResult.CertificatePkcs != null)
                            {
                                deviceCertificate = this.m_certificateGenerator.Combine(submissionResult.GetCertificiate(), privateKeyPair);
                                this.m_securityConfiguration.Signatures.RemoveAll(o => o.KeyName == "default");
                                this.m_securityConfiguration.Signatures.Add(new SecuritySignatureConfiguration("default", StoreLocation.CurrentUser, StoreName.My, deviceCertificate));
                                this.m_securityConfiguration.Signatures.Add(new SecuritySignatureConfiguration("jwsdefault", StoreLocation.CurrentUser, StoreName.My, deviceCertificate));
                                X509CertificateUtils.InstallCertificate(StoreName.My, deviceCertificate);
                                audit.WithSystemObjects(Core.Model.Audit.AuditableObjectRole.SecurityResource, Core.Model.Audit.AuditableObjectLifecycle.Creation, deviceCertificate);
                            }
                            else
                            {
                                throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_CERTIFICATE_HOLD, new { message = submissionResult.Message, status = submissionResult.Status }));
                            }
                        }

                        authEpConfiguration.Binding.Security.CredentialProvider = null; // No need for credentials on OAUTH since the certificate is our credential

                        // Update other configurations to use our shiny new device certificate
                        foreach(var ep in this.m_restConfiguration.Client)
                        {
                            if(ep.Binding.Security.Mode == Core.Http.Description.SecurityScheme.ClientCertificate)
                            {
                                ep.Binding.Security.ClientCertificate = new Core.Security.Configuration.X509ConfigurationElement(deviceCertificate);
                            }
                        }
                    }
                    EntitySource.Current = new EntitySource(new RepositoryEntitySource());
                }

               

                // Now we want to save the configuration
                this.m_configuration.Realm = new UpstreamRealmConfiguration(targetRealm);
                this.RealmChanged?.Invoke(this, new UpstreamRealmChangedEventArgs(targetRealm));
                this.m_upstreamSettings = new ConfiguredUpstreamRealmSettings(this.m_configuration);
                audit.WithOutcome(Core.Model.Audit.OutcomeIndicator.Success);
            }
            catch (Exception e)
            {
                audit.WithOutcome(Core.Model.Audit.OutcomeIndicator.MinorFail);
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_ERR), e);
            }
            finally
            {
                audit.Send();
            }
        }

        /// <summary>
        /// Create a device entity <paramref name="forDevice"/>
        /// </summary>
        private Entity CreateDeviceEntity(SecurityDevice forDevice)
        {
            // Create device registration information (the operating system, etc.)
            using (var hdsiRest = this.m_restClientFactory.GetRestClientFor(ServiceEndpointType.HealthDataService))
            {

                var deviceQuery = QueryExpressionBuilder.BuildQuery<DeviceEntity>(o => o.SecurityDeviceKey == forDevice.Key && o.ObsoletionTime == null);
                var deviceEntity = hdsiRest.Get<Bundle>(typeof(DeviceEntity).GetSerializationName(), deviceQuery).Item.OfType<DeviceEntity>().FirstOrDefault() ??
                    new DeviceEntity()
                    {
                        Key = Guid.NewGuid(),
                        SecurityDeviceKey = forDevice.Key
                    };

                deviceEntity.GeoTag = this.m_geographicLocationService?.GetCurrentPosition();
                deviceEntity.OperatingSystemName = $"{this.m_operatingSystemInfo.OperatingSystem}/{this.m_operatingSystemInfo.VersionString}";
                deviceEntity.ManufacturerModelName = this.m_operatingSystemInfo.ManufacturerName;
                deviceEntity.Names = new List<EntityName>()
                {
                    new EntityName(NameUseKeys.Assigned, forDevice.Name),
                    new EntityName(NameUseKeys.Search, this.m_operatingSystemInfo.MachineName)
                };

                return hdsiRest.Post<DeviceEntity, DeviceEntity>($"{typeof(DeviceEntity).GetSerializationName()}/{deviceEntity.Key}", deviceEntity);
            }
        }

        /// <summary>
        /// Generate rest client
        /// </summary>
        /// <param name="realmOptions">The realm options returned by the server</param>
        /// <returns>The collection of rest descriptions</returns>
        private IEnumerable<RestClientDescriptionConfiguration> GetRestClients(IUpstreamRealmSettings targetRealm, ServiceOptions realmOptions)
        {
            foreach (var endpoint in realmOptions.Endpoints.Where(r => r.BaseUrl.Any(k => new Uri(k).Scheme.StartsWith("http"))))
            {
                yield return new RestClientDescriptionConfiguration()
                {
                    Binding = new RestClientBindingConfiguration()
                    {
                        Security = new RestClientSecurityConfiguration()
                        {
                            AuthRealm = targetRealm.Realm.Host,
                            Mode = endpoint.SecurityScheme,
                            PreemptiveAuthentication = true,
                            CredentialProvider = endpoint.SecurityScheme.HasFlag(Core.Http.Description.SecurityScheme.Bearer) || endpoint.SecurityScheme.HasFlag(Core.Http.Description.SecurityScheme.ClientCertificate) ?
                                (ICredentialProvider)new UpstreamPrincipalCredentialProvider() : endpoint.ServiceType == ServiceEndpointType.AuthenticationService ? (ICredentialProvider)new UpstreamDeviceCredentialProvider() : null,
                            CertificateValidatorXml = new Core.Configuration.TypeReferenceConfiguration(typeof(UserInterface.UserInterfaceCertificateValidator))
                            
                        },
                        ContentTypeMapper = new DefaultContentTypeMapper(),
                        OptimizationMethod = endpoint.Capabilities.HasFlag(ServiceEndpointCapabilities.Compression) ? Core.Http.Description.HttpCompressionAlgorithm.Gzip : Core.Http.Description.HttpCompressionAlgorithm.None,
                        CompressRequests = endpoint.Capabilities.HasFlag(ServiceEndpointCapabilities.Compression)
                    },
                    Accept = endpoint.Capabilities.HasFlag(ServiceEndpointCapabilities.InternalApi) ? "application/xml" : "application/json",
                    Endpoint = endpoint.BaseUrl.Select(o => new RestClientEndpointConfiguration(o, new TimeSpan(0, 1, 0))).ToList(),
                    Name = endpoint.ServiceType.ToString(),
                    Trace = false
                };
            }
        }


        /// <inheritdoc/>
        public void UnJoin()
        {
            if (!IsConfigured())
            {
                throw new InvalidOperationException(ErrorMessages.UPSTREAM_NOT_CONFIGURED);
            }
            throw new NotImplementedException();
        }

    }
}
