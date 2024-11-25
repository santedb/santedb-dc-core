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
using RestSrvr;
using SanteDB;
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Http;
using SanteDB.Client.Services;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Http;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Certs;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Rest.OAuth.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

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
        private readonly ApplicationServiceContextConfigurationSection m_applicationConfiguration;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(DefaultUpstreamManagementService));
        private readonly IGeographicLocationProvider m_geographicLocationService;
        private readonly IPlatformSecurityProvider m_platformSecurityProvider;
        private IAuditService m_auditService;
        private readonly SecurityConfigurationSection m_securityConfiguration;
        private readonly OAuthConfigurationSection m_oauthConfigurationSection;

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUpstreamManagementService(
            IRestClientFactory restClientFactory,
            IConfigurationManager configurationManager,
            ILocalizationService localizationService,
            IServiceManager serviceManager,
            IOperatingSystemInfoService operatingSystemInfoService,
            IPlatformSecurityProvider platformSecurityProvider,
            IGeographicLocationProvider geographicLocationProvider = null,
            ICertificateGeneratorService certificateGenerator = null,
            IPolicyEnforcementService pepService = null
            )
        {
            this.m_restClientFactory = restClientFactory;
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>();
            this.m_restConfiguration = configurationManager.GetSection<RestClientConfigurationSection>();
            this.m_applicationConfiguration = configurationManager.GetSection<ApplicationServiceContextConfigurationSection>();
            this.m_securityConfiguration = configurationManager.GetSection<SecurityConfigurationSection>();
            this.m_oauthConfigurationSection = configurationManager.GetSection<OAuthConfigurationSection>();
            this.m_localizationService = localizationService;
            this.m_policyEnforcementService = pepService;
            this.m_serviceManager = serviceManager;
            this.m_certificateGenerator = certificateGenerator;
            this.m_operatingSystemInfo = operatingSystemInfoService;
            this.m_geographicLocationService = geographicLocationProvider;
            this.m_platformSecurityProvider = platformSecurityProvider;
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

            if (this.m_auditService == null)
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
                    .WithSensitivity(Core.Model.Attributes.ResourceSensitivityClassification.Administrative)
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
                var isCaConfigured = false;
                var allowHs256 = true;
                using (amiRestClient)
                {
                    using (var amiServiceClient = new AmiServiceClient(amiRestClient))
                    {
                        var realmOptions = amiServiceClient.Options(); // Get the options object from the server
                        dnSettings = realmOptions.Settings.Find(o => o.Key.Equals("dn", StringComparison.OrdinalIgnoreCase))?.Value;
                        isCaConfigured = realmOptions.Resources.Any(r => r.ResourceName == "Ca");
                        if (!Boolean.TryParse(realmOptions.Settings.Find(o => o.Key.Equals("forbidhs256"))?.Value, out allowHs256))
                        {
                            allowHs256 = true;
                        }

                        welcomeMessage = realmOptions.Settings.Find(o => o.Key.Equals("$welcome"))?.Value ?? this.m_localizationService.GetString(UserMessageStrings.JOIN_REALM_SUCCESS, new { realm = targetRealm.Realm.Host });

                        // Copy central app settings 
                        foreach (var set in realmOptions.Settings.Where(o => !o.Key.StartsWith("$")))
                        {
                            this.m_applicationConfiguration.AddAppSetting(set.Key, set.Value);
                        }

                        // Pull security configuration sections from the AMI - these are only disclosed when the AMI has our authentication as an administrator
                        this.m_securityConfiguration.PasswordRegex = realmOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.PasswordValidationDisclosureName)?.Value ??
                            this.m_securityConfiguration.PasswordRegex;
                        this.m_securityConfiguration.SetPolicy(Core.Configuration.SecurityPolicyIdentification.RequireMfa, Boolean.Parse(realmOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.RequireMfaName)?.Value ?? "false"));
                        this.m_securityConfiguration.SetPolicy(Core.Configuration.SecurityPolicyIdentification.SessionLength, TimeSpan.Parse(realmOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.LocalSessionLengthDisclosureName)?.Value ?? "00:30:00"));
                        this.m_securityConfiguration.SetPolicy(Core.Configuration.SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts, Boolean.Parse(realmOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.LocalAccountAllowedDisclosureName)?.Value ?? "false"));
                        this.m_securityConfiguration.SetPolicy(Core.Configuration.SecurityPolicyIdentification.AllowPublicBackups, Boolean.Parse(realmOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.PublicBackupsAllowedDisclosureName)?.Value ?? "false"));

                        // If the server allows for local user accounts then we will allow the application credential to be obtained in the UI
                        this.m_oauthConfigurationSection.AllowClientOnlyGrant = this.m_securityConfiguration.GetSecurityPolicy(SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts, false);

                        // Is the server compatible?
                        if (!replaceExistingRegistration &&
                            (!Version.TryParse(realmOptions.InterfaceVersion, out var ifVersion) ||
                            !ifVersion.IsCompatible(this.GetType().Assembly.GetName().Version)))
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
                    var upstreamDevice = amiClient.GetDevices(o => o.Name.ToLowerInvariant() == deviceCredential.CredentialName.ToLowerInvariant()).CollectionItem.OfType<SecurityDeviceInfo>().FirstOrDefault()?.Entity;
                    if (upstreamDevice != null && !replaceExistingRegistration)
                    {
                        throw new DuplicateNameException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_DEVICE_DUPLICATE, new { device = upstreamDevice.Name }));
                    }
                    else if (upstreamDevice != null)
                    {
                        upstreamDevice.DeviceSecret = deviceCredential.CredentialSecret;
                        upstreamDevice = amiClient.UpdateDevice(upstreamDevice.Key.Value, new SecurityDeviceInfo(upstreamDevice))?.Entity;
                        audit.WithSystemObjects(AuditableObjectRole.SecurityUser, AuditableObjectLifecycle.Amendment, upstreamDevice);
                    }
                    else
                    {
                        upstreamDevice = amiClient.CreateDevice(new SecurityDeviceInfo(new Core.Model.Security.SecurityDevice()
                        {
                            DeviceSecret = deviceCredential.CredentialSecret,
                            Name = deviceCredential.CredentialName
                        }))?.Entity;
                        audit.WithSystemObjects(AuditableObjectRole.SecurityUser, AuditableObjectLifecycle.Amendment, upstreamDevice);
                    }

                    this.m_securityConfiguration.SetPolicy(SecurityPolicyIdentification.AssignedDeviceSecurityId, upstreamDevice.Key.Value);

                    var entity = this.CreateDeviceEntity(upstreamDevice);
                    this.m_securityConfiguration.SetPolicy(SecurityPolicyIdentification.AssignedDeviceEntityId, entity.Key.Value);


                    audit.WithIdentifiedData(AuditableObjectLifecycle.Creation, entity);
                    var authEpConfiguration = this.m_restConfiguration.Client.Find(o => o.Name == ServiceEndpointType.AuthenticationService.ToString());
                    if (targetRealm.Realm.Scheme == "https" &&
                        authEpConfiguration.Binding.Security.Mode == Core.Http.Description.SecurityScheme.ClientCertificate)
                    {

                        var deviceSubjectName = $"CN={upstreamDevice.Key}, DC={targetRealm.LocalDeviceName}, DC={targetRealm.Realm.Host}";
                        if (!String.IsNullOrEmpty(dnSettings))
                        {
                            deviceSubjectName += $", {dnSettings}";
                        }

                        deviceCredential.CertificateSecret = new Core.Security.Configuration.X509ConfigurationElement(StoreLocation.CurrentUser, StoreName.My, X509FindType.FindBySubjectDistinguishedName, deviceSubjectName);

                        // Is there already a certificate that has a private key?
                        if (!this.m_platformSecurityProvider.TryGetCertificate(X509FindType.FindBySubjectDistinguishedName, deviceSubjectName, StoreName.My, out var deviceCertificate) ||
                            !deviceCertificate.Verify()) // No certificate
                        {
                            this.m_tracer.TraceInfo("Will generate certificate with subject: {0}", deviceSubjectName);

                            if (this.m_certificateGenerator == null)
                            {
                                throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_CANNOT_GENERATE_CERTIFICATE));
                            }

                            var privateKeyPair = this.m_certificateGenerator.CreateKeyPair(2048);
                            if (isCaConfigured)
                            {
                                var csr = this.m_certificateGenerator.CreateSigningRequest(privateKeyPair, new X500DistinguishedName(deviceSubjectName), X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement, new string[] { ExtendedKeyUsageOids.ClientAuthentication });
                                var submissionResult = amiClient.SubmitCertificateSigningRequest(new Core.Model.AMI.Security.SubmissionRequest(csr, AuthenticationContext.Current.Principal));
                                if (submissionResult.Status == Core.Model.AMI.Security.SubmissionStatus.Issued &&
                                    submissionResult.CertificatePkcs != null)
                                {
                                    deviceCertificate = this.m_certificateGenerator.Combine(submissionResult.GetCertificiate(), privateKeyPair);
                                    _ = this.m_platformSecurityProvider.TryInstallCertificate(deviceCertificate);
                                    audit.WithSystemObjects(Core.Model.Audit.AuditableObjectRole.SecurityResource, Core.Model.Audit.AuditableObjectLifecycle.Creation, deviceCertificate);
                                }
                                else
                                {
                                    throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_CERTIFICATE_HOLD, new { message = submissionResult.Message, status = submissionResult.Status }));
                                }
                            }
                            else
                            {
                                deviceCertificate = this.m_certificateGenerator.CreateSelfSignedCertificate(privateKeyPair, new X500DistinguishedName(deviceSubjectName), new TimeSpan(365, 0, 0, 0), X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement, new string[] { ExtendedKeyUsageOids.ClientAuthentication });
                                _ = this.m_platformSecurityProvider.TryInstallCertificate(deviceCertificate);
                                audit.WithSystemObjects(Core.Model.Audit.AuditableObjectRole.SecurityResource, Core.Model.Audit.AuditableObjectLifecycle.Creation, deviceCertificate);
                            }

                            this.m_tracer.TraceWarning("Installed Device Certificate: {0} (PK: {1})", deviceCertificate.Subject, deviceCertificate.HasPrivateKey);
                            if (!deviceCertificate.HasPrivateKey)
                            {
                                this.m_tracer.TraceWarning("Installed Device Certificate: {0} (PK: {1})", deviceCertificate.Subject, deviceCertificate.HasPrivateKey);
                                throw new InvalidOperationException("Device certificate did not have a private key - ensure you are running on a supported platform");
                            }

                            // Attempt to send the device credential to the server
                            try
                            {
                                this.m_tracer.TraceInfo("Sending authentication public key to server");
                                amiClient.Client.Post<X509Certificate2Info, X509Certificate2Info>($"SecurityDevice/{upstreamDevice.Key}/auth_cert", new X509Certificate2Info(deviceCertificate));
                            }
                            catch (Exception e)
                            {
                                this.m_tracer.TraceError("Could not send authentication certificate to server - administrator will need to manually add it - {0}", e);
                            }
                        }

                        authEpConfiguration.Binding.Security.CredentialProvider = null; // No need for credentials on OAUTH since the certificate is our credential

                        // Update other configurations to use our shiny new device certificate
                        foreach (var ep in this.m_restConfiguration.Client)
                        {
                            if (ep.Binding.Security.Mode == Core.Http.Description.SecurityScheme.ClientCertificate)
                            {
                                ep.Binding.Security.ClientCertificate = new Core.Security.Configuration.X509ConfigurationElement(deviceCertificate);
                            }
                        }
                    }


                    // Generate a signing certificate for data on this machine
                    var subjectName = $"CN={targetRealm.LocalDeviceName} Digital Signature, DC={targetRealm.LocalDeviceName}, DC={targetRealm.Realm.Host}";
                    if (!String.IsNullOrEmpty(dnSettings))
                    {
                        subjectName += $", {dnSettings}";
                    }

                    if (!this.m_platformSecurityProvider.TryGetCertificate(X509FindType.FindBySubjectDistinguishedName, subjectName, StoreName.My, out var signingCertificate))
                    {
                        if (this.m_certificateGenerator == null)
                        {
                            throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_CANNOT_GENERATE_CERTIFICATE));
                        }

                        var privateKeyPair = this.m_certificateGenerator.CreateKeyPair(2048);
                        if (isCaConfigured)
                        {
                            var csr = this.m_certificateGenerator.CreateSigningRequest(privateKeyPair, new X500DistinguishedName(subjectName), X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.DigitalSignature, new string[] { ExtendedKeyUsageOids.CodeSigning });
                            var submissionResult = amiClient.SubmitCertificateSigningRequest(new Core.Model.AMI.Security.SubmissionRequest(csr, AuthenticationContext.Current.Principal));
                            if (submissionResult.Status == Core.Model.AMI.Security.SubmissionStatus.Issued &&
                                submissionResult.CertificatePkcs != null)
                            {
                                signingCertificate = this.m_certificateGenerator.Combine(submissionResult.GetCertificiate(), privateKeyPair);
                                _ = this.m_platformSecurityProvider.TryInstallCertificate(signingCertificate, StoreName.My);
                                audit.WithSystemObjects(Core.Model.Audit.AuditableObjectRole.SecurityResource, Core.Model.Audit.AuditableObjectLifecycle.Creation, signingCertificate);
                            }
                            else
                            {
                                throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_JOIN_CERTIFICATE_HOLD, new { message = submissionResult.Message, status = submissionResult.Status }));
                            }
                        }
                        else
                        {
                            signingCertificate = this.m_certificateGenerator.CreateSelfSignedCertificate(privateKeyPair, new X500DistinguishedName(subjectName), new TimeSpan(730, 0, 0, 0), X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.DigitalSignature);
                            _ = this.m_platformSecurityProvider.TryInstallCertificate(signingCertificate, StoreName.My);
                            audit.WithSystemObjects(Core.Model.Audit.AuditableObjectRole.SecurityResource, Core.Model.Audit.AuditableObjectLifecycle.Creation, signingCertificate);
                        }
                    }

                    this.m_tracer.TraceWarning("Installed Signing Certificate: {0} (PK: {1})", signingCertificate.Subject, signingCertificate.HasPrivateKey);
                    if (!signingCertificate.HasPrivateKey)
                    {
                        this.m_tracer.TraceWarning("SECURITY-ALERT: Signing Certificate: {0} (PK: {1}) could not be used since it does not contain a private key!!!!", signingCertificate.Subject, signingCertificate.HasPrivateKey);
                    }
                    else
                    {
                        // Attempt to load from platform service
                        if (!this.m_platformSecurityProvider.TryGetCertificate(X509FindType.FindByThumbprint, signingCertificate.Thumbprint, out var validateCert) ||
                            !validateCert.HasPrivateKey)
                        {
                            this.m_tracer.TraceWarning("SECURITY-ALERT: The signing certificate in the platform security provider does not have a private key! Ensure that you have configured the appropriate platform security service");
                        }
                        else
                        {
                            // Remove all HMAC and replace with RS256
                            this.m_securityConfiguration.Signatures.RemoveAll(o => o.Algorithm == SignatureAlgorithm.HS256);
                            this.m_securityConfiguration.Signatures.Add(new SecuritySignatureConfiguration("default", StoreLocation.CurrentUser, StoreName.My, signingCertificate));
                        }
                    }

                    // Attempt to send the device credential to the server
                    try
                    {
                        this.m_tracer.TraceInfo("Sending signature public key to server");
                        amiClient.Client.Post<X509Certificate2Info, X509Certificate2Info>($"SecurityDevice/{upstreamDevice.Key}/dsig_cert", new X509Certificate2Info(signingCertificate));
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Could not send signature certificate to server - administrator will need to manually add it - {0}", e);
                    }

                }



                // Now we want to save the configuration
                this.m_configuration.Realm = new UpstreamRealmConfiguration(targetRealm);
                this.RealmChanged?.Invoke(this, new UpstreamRealmChangedEventArgs(targetRealm));
                this.m_upstreamSettings = new ConfiguredUpstreamRealmSettings(this.m_configuration);
                audit.WithOutcome(Core.Model.Audit.OutcomeIndicator.Success);
            }
            catch (Exception e)
            {
                audit.WithOutcome(Core.Model.Audit.OutcomeIndicator.SeriousFail);
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
        private IEnumerable<RestClientDescriptionConfiguration> GetRestClients(IUpstreamRealmSettings targetRealm, ServiceOptions realmOptions)
        {
            foreach (var endpoint in realmOptions.Endpoints.Where(r => r.BaseUrl.Any(k => new Uri(k).Scheme.StartsWith("http")) && r.ServiceType != ServiceEndpointType.Metadata && r.ServiceType != ServiceEndpointType.Other))
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
