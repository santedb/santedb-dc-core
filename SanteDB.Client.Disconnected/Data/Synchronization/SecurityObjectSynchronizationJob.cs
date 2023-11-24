using SanteDB.Client.Tickles;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl.Repository;
using SanteDB.Persistence.Data.Services.Persistence.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            IUpstreamServiceProvider<IPolicyInformationService> upstreamPolicyInformationService,
            ILocalServiceProvider<IPolicyInformationService> localPolicyInformationService,
            IUpstreamServiceProvider<IRoleProviderService> upstreamRoleProviderService,
            ILocalServiceProvider<IRoleProviderService> localRoleProviderService,
            IUpstreamServiceProvider<ISecurityRepositoryService> upstreamSecurityProviderService,
            ILocalServiceProvider<ISecurityRepositoryService> localSecurityProviderService
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
            if (_UpstreamManagementService?.IsConfigured() != true)
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
                    //TODO: Should we sync devices as well?

                    GetUpstreamSecurityRolePolicies();

                    

                    //TODO: Do we still need local notifications (tickles)?
                    _JobStateManager.SetState(this, JobStateType.Completed);
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    _JobStateManager.SetState(this, JobStateType.Aborted);
                    _JobStateManager.SetProgress(this, ex.Message, 0f);

                    //TODO: Do we still need local notifications (tickles)?

                    _Tracer.TraceWarning("Job {1}: Could not refresh system roles and policies. Exception: {0}", ex.ToString(), nameof(SecurityObjectSynchronizationJob));
                }
            }
        }

        private void GetUpstreamSecurityRolePolicies()
        {
            var systemroles = new string[] { "SYNCHRONIZERS", "ADMINISTRATORS", "ANONYMOUS", "DEVICE", "SYSTEM", "USERS", "CLINICAL_STAFF", "LOCAL_USERS" }; //TODO: Get rid of this.

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
                        continue;

                    if (null == _LocalPolicyInformationService.GetPolicy(policy.Oid))
                        _LocalPolicyInformationService.CreatePolicy(policy, AuthenticationContext.SystemPrincipal);
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Job {1}: Error synchronizing policies. Exception: {0}", ex.ToString(), nameof(SecurityObjectSynchronizationJob));
            }
        }

        private void GetUpstreamSecurityApplications()
        {
            var applications = _UpstreamSecurityApplicationRepository.Find(_ => true);

            foreach(var application in applications)
            {
                try
                {
                    if (application?.Key.HasValue != true)
                        continue;

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
                catch(Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    _Tracer.TraceWarning("Job {1}: Failed to insert/update Application {0}", application.Name, nameof(SecurityObjectSynchronizationJob));
                }
            }

        }
    }
}
