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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// Represents a policy information service which communicates with an upstream policy information service
    /// </summary>
    public class UpstreamPolicyInformationService : UpstreamServiceBase, IPolicyInformationService, IUpstreamServiceProvider<IPolicyInformationService>
    {

        /// <inheritdoc/>
        public IPolicyInformationService UpstreamProvider => this;

        private readonly ILocalizationService m_localizationSerice;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(UpstreamPolicyInformationService));

        /// <inheritdoc/>
        public string ServiceName => "Upstream Policy Information Service";

        /// <summary>
        /// DI ctor
        /// </summary>
        public UpstreamPolicyInformationService(ILocalizationService localizationService,
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider availabilityProvider,
            IUpstreamIntegrationService integrationService) : base(restClientFactory, upstreamManagementService, availabilityProvider, integrationService)
        {
            this.m_localizationSerice = localizationService;
        }

        /// <inheritdoc/>
        public void AddPolicies(object securable, PolicyGrantType rule, IPrincipal principal, params string[] policyOids)
        {
            if (securable == null)
            {
                throw new ArgumentNullException(nameof(securable));
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not conifgured - skipping policy check");
                return;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    foreach (var itm in policyOids)
                    {
                        client.AddPolicy(securable as IAnnotatedResource, itm, rule);
                    }
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN), e);

            }
        }

        /// <inheritdoc/>
        public IEnumerable<IPolicyInstance> GetPolicies(object securable)
        {
            if (securable == null)
            {
                throw new ArgumentNullException(nameof(securable));
            }
            else if (!this.IsUpstreamConfigured)
            {
                // Is this the system principal? If so - we will allow system to do anything prior to configuration
                if (securable == AuthenticationContext.SystemPrincipal)
                {
                    return typeof(PermissionPolicyIdentifiers).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                        .Select(o => new UpstreamPolicyInstance(securable, new SecurityPolicy(o.Name, (string)o.GetValue(null), false, false), PolicyGrantType.Grant))
                        .ToArray();
                }

                this.m_tracer.TraceWarning("Upstream is not conifgured - skipping policy check");
                return new IPolicyInstance[0];
            }

            try
            {
                IEnumerable<IPolicyInstance> retVal = null;
                switch (securable)
                {
                    case SecurityDevice sd:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            retVal = client.GetDevices(d => d.Name == sd.Name).CollectionItem.OfType<SecurityDeviceInfo>().FirstOrDefault()?.Policies.Select(o => new UpstreamPolicyInstance(sd, o.Policy, o.Grant));
                        }
                        break;

                    case SecurityApplication sa:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            retVal = client.GetApplications(a => a.Name == sa.Name).CollectionItem.OfType<SecurityApplicationInfo>().FirstOrDefault()?.Policies.Select(o => new UpstreamPolicyInstance(sa, o.Policy, o.Grant));
                        }
                        break;

                    case SecurityRole sr:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            retVal = client.GetRoles(a => a.Name == sr.Name).CollectionItem.OfType<SecurityRoleInfo>().FirstOrDefault()?.Policies.Select(o => new UpstreamPolicyInstance(sr, o.Policy, o.Grant));
                        }
                        break;

                    case IPrincipal ipr:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            retVal = client.GetUsers(o => o.UserName == ipr.Identity.Name).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault()?.Policies.Select(o => new UpstreamPolicyInstance(ipr, o.Policy, o.Grant));
                        }
                        break;

                    case IIdentity iid:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            retVal = client.GetUsers(o => o.UserName == iid.Name).CollectionItem.OfType<SecurityUserInfo>().FirstOrDefault()?.Policies.Select(o => new UpstreamPolicyInstance(iid, o.Policy, o.Grant));
                        }
                        break;

                    case Act act:
                        using (var client = this.CreateHdsiServiceClient())
                        {
                            retVal = client.Get<Act>(act.Key.Value, null).Policies.Select(o => new UpstreamPolicyInstance(act, o.Policy, PolicyGrantType.Grant));
                        }
                        break;

                    case Entity ent:
                        using (var client = this.CreateHdsiServiceClient())
                        {
                            retVal = client.Get<Entity>(ent.Key.Value, null).Policies.Select(o => new UpstreamPolicyInstance(ent, o.Policy, PolicyGrantType.Grant));
                        }
                        break;

                    default:
                        retVal = new IPolicyInstance[0];
                        break;
                }

                return retVal ?? new IPolicyInstance[0];
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN), e);
            }

        }

        /// <inheritdoc/>
        public IEnumerable<IPolicy> GetPolicies()
        {
            if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not conifgured - returning default list for policy check");
                return typeof(PermissionPolicyIdentifiers).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                        .Select(o => new GenericPolicy(Guid.Empty, (string)o.GetValue(null), o.Name, false))
                        .ToArray();
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    return client.FindPolicy(o => o.ObsoletionTime == null).CollectionItem.OfType<SecurityPolicy>().Select(o => new GenericPolicy(o.Key.Value, o.Oid, o.Name, o.CanOverride));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN), e);

            }
        }

        /// <inheritdoc/>
        public IPolicy GetPolicy(string policyOid)
        {
            if (!this.IsUpstreamConfigured)
            {
                return null;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    return client.FindPolicy(p => p.Oid == policyOid).CollectionItem.OfType<SecurityPolicy>().Select(o => new GenericPolicy(o.Key.Value, o.Oid, o.Name, o.CanOverride)).FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN), e);
            }
        }

        /// <inheritdoc/>
        public IPolicyInstance GetPolicyInstance(object securable, string policyOid) => this.GetPolicies(securable).FirstOrDefault(o => o.Policy.Oid == policyOid);

        /// <inheritdoc/>
        public bool HasPolicy(object securable, string policyOid) => this.GetPolicies(securable).Any(o => o.Policy.Oid == policyOid);

        /// <inheritdoc/>
        public void RemovePolicies(object securable, IPrincipal principal, params string[] oid)
        {
            if (securable == null)
            {
                throw new ArgumentNullException(nameof(securable));
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not conifgured - skipping policy check");
                return;
            }

            try
            {
                var policyKeys = this.GetPolicies(securable).Where(o => oid.Contains(o.Policy.Oid)).Select(o => o.Policy.Key).ToList();
                switch (securable)
                {
                    case SecurityDevice sd:
                        using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                        {
                            policyKeys.ForEach(o => client.Delete<object>($"SecurityDevice/{sd.Key}/policy/{o}"));
                        }
                        break;
                    case SecurityApplication sa:
                        using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                        {
                            policyKeys.ForEach(o => client.Delete<object>($"SecurityApplication/{sa.Key}/policy/{o}"));
                        }
                        break;
                    case SecurityRole sr:
                        using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                        {
                            policyKeys.ForEach(o => client.Delete<object>($"SecurityRole/{sr.Key}/policy/{o}"));
                        }
                        break;
                    default:
                        throw new NotSupportedException(String.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, securable.GetType().Name));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_ASSIGN, new { securable = securable, policyOids = String.Join(";", oid) }), e);
            }
        }

        /// <summary>
        /// Create policy
        /// </summary>
        public void CreatePolicy(IPolicy policy, IPrincipal principal)
        {
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not conifgured - skipping policy check");
                return;
            }

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                {
                    client.Post<SecurityPolicyInfo, SecurityPolicyInfo>("SecurityPolicy", new SecurityPolicyInfo(policy));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN, new { policyOids = policy.Oid }), e);
            }
        }
    }
}
