/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.IdentityModel.Abstractions;
using SanteDB;
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Http;
using SanteDB.Client.Upstream.Security;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Jobs;
using SanteDB.Core.Matching;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl.Repository;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Persistence.Data.Services.Persistence.Acts;
using SanteDB.Rest.OAuth.Configuration;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace SanteDB.Client.Disconnected.Jobs
{
    /// <summary>
    /// Synchronizes the security policies assigned to security roles from the upstream server.
    /// </summary>
    public class SecurityObjectSynchronizationJob : ISynchronizationJob
    {
        internal static readonly Guid JobInvariantId = Guid.Parse("31C2586A-6DAE-4AFB-8CFB-BAE1F4F26C3F");
        /// <inheritdoc/>
        public Guid Id => JobInvariantId;
        /// <inheritdoc/>
        public string Name => "Security Object Synchronization";
        /// <inheritdoc/>
        public string Description => "Synchronizes the security objects from an upstream realm to the local instance.";
        /// <inheritdoc/>
        public bool CanCancel => false;
        /// <inheritdoc/>
        public IDictionary<string, Type> Parameters { get; private set; } = new Dictionary<string, Type>();

        readonly Tracer _Tracer;
        readonly IJobStateManagerService _JobStateManager;
        readonly IUpstreamManagementService _UpstreamManagementService;
        private readonly IDataSigningCertificateManagerService _LocalDataSigningCertificateManager;
        private readonly IDataSigningCertificateManagerService _UpstreamDataSigningCertificateManager;
        readonly IPolicyInformationService _UpstreamPolicyInformationService;
        readonly IPolicyInformationService _LocalPolicyInformationService;
        readonly IRoleProviderService _UpstreamRoleProviderService;
        readonly IRoleProviderService _LocalRoleProviderService;
        readonly ISecurityRepositoryService _UpstreamSecurityRepositoryService;
        readonly ISecurityRepositoryService _LocalSecurityRepositoryService;
        private readonly IUpstreamAvailabilityProvider _UpstreamAvailabilityProvider;
        private readonly IRestClientFactory _RestClientFactory;
        private readonly ISynchronizationLogService _SynchronizationLogService;
        private readonly IConfigurationManager _ConfigurationManager;
        readonly ISecurityChallengeService _UpstreamSecurityChallengeService;
        readonly ISecurityChallengeService _LocalSecurityChallengeService;
        readonly IRepositoryService<SecurityApplication> _UpstreamSecurityApplicationRepository;
        readonly IRepositoryService<SecurityApplication> _LocalSecurityApplicationRepository;

        /// <summary>
        /// Dependency-Injection Constructor
        /// </summary>
        public SecurityObjectSynchronizationJob(
            IJobStateManagerService jobStateManager,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamServiceProvider<IPolicyInformationService> upstreamPolicyInformationService,
            ILocalServiceProvider<IPolicyInformationService> localPolicyInformationService,
            IUpstreamServiceProvider<IRoleProviderService> upstreamRoleProviderService,
            ILocalServiceProvider<IRoleProviderService> localRoleProviderService,
            IUpstreamServiceProvider<ISecurityRepositoryService> upstreamSecurityProviderService,
            ILocalServiceProvider<ISecurityRepositoryService> localSecurityProviderService,
            IRestClientFactory restClientFactory,
            IConfigurationManager configurationManager,
            ISynchronizationLogService synchronizationLogService,
            ILocalServiceProvider<IDataSigningCertificateManagerService> localDataSigningCertificateManager,
            ILocalServiceProvider<IRepositoryService<SecurityApplication>> localSecurityApplicationRepository = null
            //IUpstreamServiceProvider<ISecurityChallengeService> upstreamSecurityChallengeService = null,
            //ILocalServiceProvider<ISecurityChallengeService> localSecurityChallengeService = null
            )
        {
            _Tracer = Tracer.GetTracer(typeof(SecurityObjectSynchronizationJob));
            _JobStateManager = jobStateManager;
            _UpstreamManagementService = upstreamManagementService;
            _LocalDataSigningCertificateManager = localDataSigningCertificateManager.LocalProvider;
            _UpstreamDataSigningCertificateManager = typeof(UpstreamCertificateAssociationManager).CreateInjected() as IDataSigningCertificateManagerService;
            _UpstreamPolicyInformationService = upstreamPolicyInformationService.UpstreamProvider;
            _LocalPolicyInformationService = localPolicyInformationService.LocalProvider;
            _UpstreamRoleProviderService = upstreamRoleProviderService.UpstreamProvider;
            _LocalRoleProviderService = localRoleProviderService.LocalProvider;
            _UpstreamSecurityRepositoryService = upstreamSecurityProviderService.UpstreamProvider;
            _LocalSecurityRepositoryService = localSecurityProviderService.LocalProvider;
            _UpstreamAvailabilityProvider = upstreamAvailabilityProvider;
            _RestClientFactory = restClientFactory;
            _SynchronizationLogService = synchronizationLogService;
            _ConfigurationManager = configurationManager;
            _LocalSecurityApplicationRepository = localSecurityApplicationRepository?.LocalProvider;
            //_UpstreamSecurityChallengeService = upstreamSecurityChallengeService.UpstreamProvider;
            //_LocalSecurityChallengeService = localSecurityChallengeService.LocalProvider;
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            throw new NotSupportedException(ErrorMessages.NOT_SUPPORTED);
        }

        /// <inheritdoc/>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            if (_UpstreamManagementService?.IsConfigured() != true || _UpstreamAvailabilityProvider?.IsAvailable(ServiceEndpointType.AdministrationIntegrationService) != true)
            {
                _Tracer.TraceInfo("Job {0}: The upstream realm is not configured.", nameof(SecurityObjectSynchronizationJob));
                _JobStateManager.SetState(this, JobStateType.Cancelled);
                return;
            }

            using (AuthenticationContext.EnterSystemContext())
            {
                try
                {
                    _JobStateManager.SetState(this, JobStateType.Running);

                    GetUpstreamSecurityPolicies();
                    GetUpstreamSecurityApplications();
                    GetUpstreamSecurityRolePolicies();
                    GetUpstreamSigningCertificates();

                    //TODO: Do we still need local notifications (tickles)?
                    _JobStateManager.SetState(this, JobStateType.Completed);
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    _JobStateManager.SetState(this, JobStateType.Aborted, ex.ToHumanReadableString());
                    _JobStateManager.SetProgress(this, ex.Message, 0f);

                    //TODO: Do we still need local notifications (tickles)?

                    _Tracer.TraceWarning("Job {1}: Could not refresh system roles and policies. Exception: {0}", ex.ToString(), nameof(SecurityObjectSynchronizationJob));
                }
            }
        }

        private void GetUpstreamSigningCertificates()
        {
            if (this._UpstreamAvailabilityProvider.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService) && _UpstreamDataSigningCertificateManager != null)
            {
                var lastSyncLog = this._SynchronizationLogService.Get(typeof(X509Certificate2Info)) ?? this._SynchronizationLogService.Create(typeof(X509Certificate2Info));
                this._Tracer.TraceInfo("Will synchronize data signing certificates created or deleted since {0}", lastSyncLog.LastSync);

                // Fetch upstream since
                NameValueCollection activeFilter = new NameValueCollection(), obsoleteFilter = new NameValueCollection();
                if (lastSyncLog.LastSync.HasValue)
                {
                    activeFilter.Add("modifiedSince", lastSyncLog.LastSync.ToString());
                    obsoleteFilter.Add("obsoleteSince", lastSyncLog.LastSync.ToString());
                    foreach (var obsCert in _UpstreamDataSigningCertificateManager.GetSigningCertificates(typeof(SecurityDevice), obsoleteFilter))
                    {
                        _LocalDataSigningCertificateManager.RemoveSigningCertificate(AuthenticationContext.AnonymousPrincipal.Identity, obsCert, AuthenticationContext.SystemPrincipal);
                    }
                }


                var signingCerts = _UpstreamDataSigningCertificateManager.GetSigningCertificates(typeof(SecurityDevice), activeFilter);
                if (_LocalDataSigningCertificateManager is IDataSigningCertificateManagerServiceEx extendedCertificateManager)
                {
                    extendedCertificateManager.AddSigningCertificates(AuthenticationContext.AnonymousPrincipal.Identity, signingCerts, AuthenticationContext.SystemPrincipal);
                }
                else
                {
                    foreach (var cert in signingCerts)
                    {
                        _LocalDataSigningCertificateManager.AddSigningCertificate(AuthenticationContext.AnonymousPrincipal.Identity, cert, AuthenticationContext.SystemPrincipal);
                    }
                }

                this._SynchronizationLogService.Save(lastSyncLog, String.Empty, DateTime.Now);

            }
        }

        private void GetUpstreamSecurityRolePolicies()
        {
            var systemroles = new string[] {
                SanteDBConstants.AdministratorGroupName,
                SanteDBConstants.AnonymousGroupName,
                SanteDBConstants.DeviceGroupName,
                SanteDBConstants.SystemGroupName,
                SanteDBConstants.UserGroupName,
                SanteDBConstants.LocalUserGroupName,
                SanteDBConstants.LocalAdminGroupName,
                SanteDBConstants.ClinicalStaffGroupName
            }; //TODO: Get rid of this.

            // Get all policies and role information from the upstream 
            var roles = _LocalRoleProviderService.GetAllRoles().Union(systemroles).Select(o => _LocalRoleProviderService.GetAllRoles(o)).ToArray();
            NameValueCollection nvc = new NameValueCollection();
            roles.ForEach(o => nvc.Add("name", o));
            using (var amiClient = this._RestClientFactory.GetRestClientFor(ServiceEndpointType.AdministrationIntegrationService))
            {
                // Get all reference data 
                var referenceRoleData = amiClient.Get<AmiCollection>($"{nameof(SecurityRole)}", nvc);

                foreach (var upstreamRole in referenceRoleData.CollectionItem.OfType<SecurityRoleInfo>())
                {
                    try
                    {
                        var role = _LocalSecurityRepositoryService.GetRole(upstreamRole.Entity.Name);

                        if (null == role)
                        {
                            _LocalRoleProviderService.CreateRole(upstreamRole.Entity.Name, AuthenticationContext.SystemPrincipal);
                            role = _LocalSecurityRepositoryService.GetRole(upstreamRole.Entity.Name);
                        }


                        var localrolepolicies = _LocalPolicyInformationService.GetPolicies(role);
                        var removedpolicies = localrolepolicies.Where(o => !upstreamRole.Policies.Any(p => p?.Policy?.Oid == o?.Policy?.Oid));
                        _LocalPolicyInformationService.RemovePolicies(role, AuthenticationContext.SystemPrincipal, removedpolicies?.Select(o => o?.Policy?.Oid).ToArray());
                        foreach (var policy in upstreamRole.Policies)
                        {
                            if (null == _LocalPolicyInformationService.GetPolicy(policy?.Policy?.Oid))
                            {
                                _LocalPolicyInformationService.CreatePolicy(new GenericPolicy(policy.Policy.Key.Value, policy.Policy.Oid, policy.Policy.Name, policy.Policy.CanOverride, policy.IsPublic), AuthenticationContext.SystemPrincipal);
                            }

                            _LocalPolicyInformationService.AddPolicies(role, policy.Grant, AuthenticationContext.SystemPrincipal, policy.Oid);
                        }
                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        _Tracer.TraceWarning("Job {1}: Error synchronizing role {0}", upstreamRole.Entity.Name, nameof(SecurityObjectSynchronizationJob));
                    }
                }
            }
        }

        private void GetUpstreamSecurityPolicies()
        {
            try
            {
                var upstreamPolicies = _UpstreamPolicyInformationService.GetPolicies();

                if (_LocalPolicyInformationService is IPolicyInformationServiceEx extendedPolicyInformationService)
                {
                    extendedPolicyInformationService.CreatePolicies(upstreamPolicies, AuthenticationContext.SystemPrincipal);
                }
                else
                {
                    foreach (var policy in upstreamPolicies)
                    {
                        if (null == policy)
                        {
                            continue;
                        }

                        if (null == _LocalPolicyInformationService.GetPolicy(policy.Oid))
                        {
                            _LocalPolicyInformationService.CreatePolicy(policy, AuthenticationContext.SystemPrincipal);
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Job {1}: Error synchronizing policies. Exception: {0}", ex.ToString(), nameof(SecurityObjectSynchronizationJob));
            }
        }

        private void GetUpstreamSecurityApplications()
        {

            try
            {
                using (var amiClient = this._RestClientFactory.GetRestClientFor(ServiceEndpointType.AdministrationIntegrationService))
                {
                    var myAppId = _ConfigurationManager.GetSection<UpstreamConfigurationSection>().Credentials.First(o => o.CredentialType == UpstreamCredentialType.Application)?.CredentialName;
                    var systemSid = Guid.Parse(AuthenticationContext.SystemApplicationSid);
                    var localRegisteredApps = _LocalSecurityApplicationRepository?.Find(o => o.ObsoletionTime == null).ToArray().Select(o => o.Name).ToArray() ?? new String[] { myAppId };
                    var amiQuery = String.Join("&", localRegisteredApps.Select(o => $"name={o}")).ParseQueryString();
                    var appinfo = amiClient.Get<AmiCollection>($"{typeof(SecurityApplication).GetSerializationName()}", amiQuery).CollectionItem.OfType<SecurityApplicationInfo>();

                    // Set the policies 
                    foreach (var app in appinfo)
                    {
                        foreach (var pol in app.Policies.GroupBy(o => o.Grant))
                        {
                            _LocalPolicyInformationService.AddPolicies(app.Entity, pol.Key, AuthenticationContext.SystemPrincipal, pol.Select(o => o.Policy.Oid).ToArray());
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Job {1}: Error synchronizing application. Exception: {0}", ex.ToString(), nameof(SecurityObjectSynchronizationJob));
            }

        }
    }
}
