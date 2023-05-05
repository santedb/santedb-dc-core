using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
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
    /// Policy information service that uses either local or upstream policy provider.
    /// </summary>
    [PreferredService(typeof(IPolicyInformationService))]
    public class UpstreamPolicyInformationProvider : UpstreamServiceBase, IPolicyInformationService
    {
        readonly ILocalServiceProvider<IPolicyInformationService> _localPolicyProvider;
        readonly IUpstreamServiceProvider<IPolicyInformationService> _upstreamPolicyProvider;
        readonly IAdhocCacheService _CacheService;
        readonly bool _CanSynchronize;
        readonly Tracer _Tracer;
        readonly TimeSpan _CacheTimeout;

        public UpstreamPolicyInformationProvider(
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            ILocalServiceProvider<IPolicyInformationService> localPolicyProvider,
            IUpstreamServiceProvider<IPolicyInformationService> upstreamPolicyProvider,
            IAdhocCacheService cacheService,
            IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            _Tracer = new Tracer(nameof(UpstreamPolicyInformationProvider));
            _localPolicyProvider = localPolicyProvider;
            _upstreamPolicyProvider = upstreamPolicyProvider;
            _CacheTimeout = TimeSpan.FromSeconds(120); //TODO: Make this a configuration setting?
            _CacheService = cacheService;

            _CanSynchronize = null != _localPolicyProvider && _upstreamPolicyProvider != _localPolicyProvider;
        }

        /// <inheritdoc />
        public string ServiceName => nameof(UpstreamPolicyInformationService);


        /// <inheritdoc />
        public void AddPolicies(object securable, PolicyGrantType rule, IPrincipal principal, params string[] policyOids)
        {
            if (IsUpstreamConfigured && null != _upstreamPolicyProvider?.UpstreamProvider)
            {
                try
                {
                    _upstreamPolicyProvider.UpstreamProvider.AddPolicies(securable, rule, principal, policyOids);

                    //Upstream succeeded, add to local.
                    _localPolicyProvider?.LocalProvider?.AddPolicies(securable, rule, principal, policyOids);
                }
                catch (UpstreamIntegrationException)
                {
                    _Tracer.TraceError("[LOCALIZE ME] Can't add policy while offline.");
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public void CreatePolicy(IPolicy policy, IPrincipal principal)
        {
            if (IsUpstreamConfigured && null != _upstreamPolicyProvider?.UpstreamProvider)
            {
                try
                {
                    _upstreamPolicyProvider.UpstreamProvider.CreatePolicy(policy, principal);

                    //Upstream succeeded, add to local.
                    _localPolicyProvider?.LocalProvider?.CreatePolicy(policy, principal);
                }
                catch (UpstreamIntegrationException)
                {
                    _Tracer.TraceError("[LOCALIZE ME] Can't create policy while offline.");
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<IPolicyInstance> GetPolicies(object securable)
        {
            var cachekey = GetCacheKey(nameof(GetPolicies), securable);

            if (null != cachekey && _CacheService?.TryGet<IEnumerable<IPolicyInstance>>(cachekey, out var cacheval) == true)
            {
                return cacheval;
            }

            var retval = _localPolicyProvider?.LocalProvider?.GetPolicies(securable);

            if (null == retval && IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                try
                {
                    retval = _upstreamPolicyProvider?.UpstreamProvider?.GetPolicies(securable);

                    if (null != cachekey)
                    {
                        _CacheService?.Add(cachekey, retval, _CacheTimeout);
                    }
                }
                catch (UpstreamIntegrationException)
                {
                    retval = Enumerable.Empty<IPolicyInstance>();
                }
            }

            return retval;
        }

        /// <inheritdoc />
        public IEnumerable<IPolicy> GetPolicies()
        {
            var cachekey = GetCacheKey(nameof(GetPolicies)); //Cache key should never be null here since we're using a constant expression.

            if (_CacheService?.TryGet<IEnumerable<IPolicy>>(cachekey, out var cacheresult) == true)
            {
                return cacheresult;
            }

            var retval = _localPolicyProvider?.LocalProvider?.GetPolicies();

            if (null == retval && IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                try
                {
                    retval = _upstreamPolicyProvider?.UpstreamProvider?.GetPolicies();

                    _CacheService?.Add(cachekey, retval, _CacheTimeout);
                }
                catch (UpstreamIntegrationException)
                {
                    retval = Enumerable.Empty<IPolicy>();
                }
            }

            return retval;
        }

        /// <inheritdoc />
        public IPolicy GetPolicy(string policyOid)
        {
            var cachekey = GetCacheKey(nameof(GetPolicy), policyOid); //Cache key should never be null here because we're using a string expression.

            if (_CacheService?.TryGet<IPolicy>(cachekey, out var cacheresult) == true)
            {
                return cacheresult;
            }

            var retval = _localPolicyProvider?.LocalProvider?.GetPolicy(policyOid);

            if (null == retval && IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                try
                {
                    retval = _upstreamPolicyProvider?.UpstreamProvider?.GetPolicy(policyOid);

                    _CacheService?.Add(cachekey, retval, _CacheTimeout);
                }
                catch (UpstreamIntegrationException)
                {
                    _Tracer.TraceError("[LOCALIZE ME] Exception getting upstream policy for {0}", policyOid);
                    throw;
                }
            }

            return retval;
        }

        /// <inheritdoc />
        public IPolicyInstance GetPolicyInstance(object securable, string policyOid)
        {
            var cachekey = GetCacheKey(nameof(GetPolicyInstance), securable, policyOid);

            var retval = _localPolicyProvider?.LocalProvider?.GetPolicyInstance(securable, policyOid);

            if (null == retval && IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                try
                {
                    retval = _upstreamPolicyProvider?.UpstreamProvider?.GetPolicyInstance(securable, policyOid);
                }
                catch (UpstreamIntegrationException)
                {
                    _Tracer.TraceError("[LOCALIZE ME] Exception getting upstream policy for {0}", securable.ToString());
                    throw;
                }
            }

            return retval;
        }

        /// <inheritdoc />
        public bool HasPolicy(object securable, string policyOid)
        {
            var cachekey = GetCacheKey(nameof(HasPolicy), securable, policyOid);

            if (null != cachekey && _CacheService?.TryGet<bool>(cachekey, out var cacheresult) == true)
            {
                return cacheresult;
            }

            var retval = _localPolicyProvider?.LocalProvider?.HasPolicy(securable, policyOid);

            if (null == retval && IsUpstreamAvailable(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
            {
                try
                {
                    retval = _upstreamPolicyProvider?.UpstreamProvider?.HasPolicy(securable, policyOid);

                    if (null != cachekey)
                    {
                        _CacheService?.Add(cachekey, retval, _CacheTimeout);
                    }
                }
                catch (UpstreamIntegrationException)
                {
                    _Tracer.TraceError("[LOCALIZE ME] Exception checking if policy is defined for {0}.", securable.ToString());
                    throw;
                }
            }

            return retval ?? false;
        }

        /// <inheritdoc />
        public void RemovePolicies(object securable, IPrincipal principal, params string[] oid)
        {
            if (IsUpstreamConfigured && null != _upstreamPolicyProvider?.UpstreamProvider)
            {
                try
                {
                    _upstreamPolicyProvider.UpstreamProvider.RemovePolicies(securable, principal, oid);

                    //Upstream succeeded, add to local.
                    _localPolicyProvider?.LocalProvider?.RemovePolicies(securable, principal, oid);
                }
                catch (UpstreamIntegrationException)
                {
                    _Tracer.TraceError("[LOCALIZE ME] Can't remove policies while offline.");
                    throw;
                }
            }
        }

        private string GetCacheKey(params object[] objs)
        {
            if (objs == null || objs.Length == 0)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder(25); //TODO: 25 was chosen arbitrarily. Do some testing on real world values to tune this parameter.

            foreach (var obj in objs)
            {
                switch (obj)
                {
                    case IIdentifiedResource res when (res.Key != null):
                        sb.Append(res.Key.Value);
                        if (res.Tag != null)
                        {
                            sb.AppendFormat("({0})", res.Tag);
                        }
                        break;
                    case string s:
                        sb.Append(s);
                        break;
                    case Guid g:
                        sb.Append(g);
                        break;
                    default:
                        break;
                }
                sb.Append(".");
            }

            if (sb.Length > 0)
            {
                //TODO: Optimize this.
                sb.Insert(0, $"{nameof(UpstreamPolicyInformationProvider)}.");

                return sb.ToString();
            }
            else
            {
                return null;
            }
        }
    }
}
