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
 * User: fyfej
 * Date: 2024-1-23
 */
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.AMI.Client;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Synchronizes the security policies assigned to security roles from the upstream server.
    /// </summary>
    public class SecurityObjectSynchronizationJob : ISynchronizationJob
    {
        private static readonly Guid JobInvariantId = Guid.Parse("31C2586A-6DAE-4AFB-8CFB-BAE1F4F26C3F");
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
        readonly IPolicyInformationService _UpstreamPolicyInformationService;
        readonly IPolicyInformationService _LocalPolicyInformationService;
        readonly IRoleProviderService _UpstreamRoleProviderService;
        readonly IRoleProviderService _LocalRoleProviderService;
        readonly ISecurityRepositoryService _UpstreamSecurityRepositoryService;
        readonly ISecurityRepositoryService _LocalSecurityRepositoryService;
        private readonly IUpstreamAvailabilityProvider _UpstreamAvailabilityProvider;
        private readonly IRestClientFactory _RestClientFactory;
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
            IConfigurationManager configurationManager
            //IUpstreamServiceProvider<ISecurityChallengeService> upstreamSecurityChallengeService = null,
            //ILocalServiceProvider<ISecurityChallengeService> localSecurityChallengeService = null
            )
        {
            _Tracer = Tracer.GetTracer(typeof(SecurityObjectSynchronizationJob));
            _JobStateManager = jobStateManager;
            _UpstreamManagementService = upstreamManagementService;
            _UpstreamPolicyInformationService = upstreamPolicyInformationService.UpstreamProvider;
            _LocalPolicyInformationService = localPolicyInformationService.LocalProvider;
            _UpstreamRoleProviderService = upstreamRoleProviderService.UpstreamProvider;
            _LocalRoleProviderService = localRoleProviderService.LocalProvider;
            _UpstreamSecurityRepositoryService = upstreamSecurityProviderService.UpstreamProvider;
            _LocalSecurityRepositoryService = localSecurityProviderService.LocalProvider;
            _UpstreamAvailabilityProvider = upstreamAvailabilityProvider;
            _RestClientFactory = restClientFactory;
            _ConfigurationManager = configurationManager;
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
            if (_UpstreamManagementService?.IsConfigured() != true || _UpstreamAvailabilityProvider?.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService) != true)
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

                    GetUpstreamSecuritySettings();

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

        /// <summary>
        /// Gets the upstream security configuration policies that may have changed (allowing local users, mandating MFA, etc.)
        /// </summary>
        private void GetUpstreamSecuritySettings()
        {
            if(this._UpstreamAvailabilityProvider.IsAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                // Use an options
                using (var client = this._RestClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                using (var amiServiceClient = new AmiServiceClient(client))
                {
                    var serviceOptions = amiServiceClient.Options();
                    var securitySettings = _ConfigurationManager.GetSection<SecurityConfigurationSection>();
                    securitySettings.PasswordRegex = serviceOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.PasswordValidationDisclosureName)?.Value ??
                            securitySettings.PasswordRegex;
                    securitySettings.SetPolicy(Core.Configuration.SecurityPolicyIdentification.RequireMfa, Boolean.Parse(serviceOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.RequireMfaName)?.Value ?? "false"));
                    securitySettings.SetPolicy(Core.Configuration.SecurityPolicyIdentification.SessionLength, TimeSpan.Parse(serviceOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.LocalSessionLengthDisclosureName)?.Value ?? "00:30:00"));
                    securitySettings.SetPolicy(Core.Configuration.SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts, Boolean.Parse(serviceOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.LocalAccountAllowedDisclosureName)?.Value ?? "false"));
                    securitySettings.SetPolicy(Core.Configuration.SecurityPolicyIdentification.AllowPublicBackups, Boolean.Parse(serviceOptions.Settings.Find(o => o.Key == SecurityConfigurationSection.PublicBackupsAllowedDisclosureName)?.Value ?? "false"));

                    // Get the general configuration and set them 
                    var appSetting = _ConfigurationManager.GetSection<ApplicationServiceContextConfigurationSection>();
                    serviceOptions.Settings.Where(o => o.Key.StartsWith("dcdr.")).ForEach(o => appSetting.AddAppSetting(o.Key.Substring(5), o.Value));
                    _ConfigurationManager.SaveConfiguration(restart: false);
                }
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

            foreach (var rolename in _LocalRoleProviderService.GetAllRoles().Union(systemroles))
            {
                try
                {
                    var role = _LocalSecurityRepositoryService.GetRole(rolename);

                    if (null == role)
                    {
                        _LocalRoleProviderService.CreateRole(rolename, AuthenticationContext.SystemPrincipal);
                        role = _LocalSecurityRepositoryService.GetRole(rolename);
                    }

                    var activepolicies = _UpstreamPolicyInformationService.GetPolicies(role);

                    foreach (var policy in activepolicies)
                    {
                        if (null == _LocalPolicyInformationService.GetPolicy(policy?.Policy?.Oid))
                        {
                            _LocalPolicyInformationService.CreatePolicy(policy.Policy, AuthenticationContext.SystemPrincipal);
                        }
                    }

                    var localrolepolicies = _LocalPolicyInformationService.GetPolicies(role);

                    var removedpolicies = localrolepolicies.Where(o => !activepolicies.Any(p => p?.Policy?.Oid == o?.Policy?.Oid));

                    _LocalPolicyInformationService.RemovePolicies(role, AuthenticationContext.SystemPrincipal, removedpolicies?.Select(o => o?.Policy?.Oid).ToArray());

                    foreach (var pgroup in activepolicies.GroupBy(o => o.Rule))
                    {
                        _LocalPolicyInformationService.AddPolicies(role, pgroup.Key, AuthenticationContext.SystemPrincipal, pgroup.Select(o => o.Policy.Oid).ToArray());
                    }



                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    _Tracer.TraceWarning("Job {1}: Error synchronizing role {0}", rolename, nameof(SecurityObjectSynchronizationJob));
                }
            }
        }

        private void GetUpstreamSecurityPolicies()
        {
            try
            {
                foreach (var policy in _UpstreamPolicyInformationService.GetPolicies())
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
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Job {1}: Error synchronizing policies. Exception: {0}", ex.ToString(), nameof(SecurityObjectSynchronizationJob));
            }
        }

        private void GetUpstreamSecurityApplications()
        {
            if (null == _UpstreamSecurityApplicationRepository) //SKIP Since there is no upstream repository for security applications.
            {
                return;
            }

            var applications = _UpstreamSecurityApplicationRepository.Find(_ => true);

            foreach (var application in applications)
            {
                try
                {
                    if (application?.Key.HasValue != true)
                    {
                        continue;
                    }

                    var localapp = _LocalSecurityApplicationRepository.Get(application.Key.Value);

                    if (null == localapp)
                    {
                        var newapp = application.DeepCopy() as SecurityApplication;

                        localapp = _LocalSecurityApplicationRepository.Insert(newapp);
                    }


                    if (localapp.Lockout != application.Lockout)
                    {
                        localapp.Lockout = application.Lockout;
                        localapp = _LocalSecurityApplicationRepository.Save(localapp);
                    }

                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    _Tracer.TraceWarning("Job {1}: Failed to insert/update Application {0}", application.Name, nameof(SecurityObjectSynchronizationJob));
                }
            }

        }
    }
}
