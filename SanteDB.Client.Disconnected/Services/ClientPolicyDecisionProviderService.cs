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
using SanteDB.Core;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Disconnected.Services
{
    /// <summary>
    /// Represents a PDP service which can map between server permissions and the client permissions
    /// </summary>
    public class ClientPolicyDecisionProviderService : DefaultPolicyDecisionService, IPolicyDecisionServiceEx
    {

        private readonly IDictionary<String, String[]> m_policyMaps = new Dictionary<String, String[]>()
        {
            // Principal having ALTER LOCAL IDENTITY infers permissions to alter identities on this client
            { 
                PermissionPolicyIdentifiers.AlterLocalIdentity, new string[]
                {
                    PermissionPolicyIdentifiers.AlterIdentity,
                    PermissionPolicyIdentifiers.ChangePassword,
                    PermissionPolicyIdentifiers.AlterRoles
                }
            },
            // Principal having CREATE LOCAL IDENTITY infers permissions to create identities on this client
            {
                PermissionPolicyIdentifiers.CreateLocalIdentity, new string[]
                {
                    PermissionPolicyIdentifiers.CreateDevice,
                    PermissionPolicyIdentifiers.CreateApplication,
                    PermissionPolicyIdentifiers.CreateIdentity
                }
            },
            // Principal having Unrestricted Client infers permissions to alter system configurations
            {
                PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, new string[]
                {
                    PermissionPolicyIdentifiers.UnrestrictedJobManagement,
                    PermissionPolicyIdentifiers.UnrestrictedPubSub,
                    PermissionPolicyIdentifiers.UnrestrictedServiceLogs,
                    PermissionPolicyIdentifiers.UnrestrictedWarehouse,
                    PermissionPolicyIdentifiers.AlterSecurityChallenge,
                    PermissionPolicyIdentifiers.AlterSystemConfiguration,
                    PermissionPolicyIdentifiers.AssignCertificateToIdentity,
                    PermissionPolicyIdentifiers.ManageBackups,
                    PermissionPolicyIdentifiers.CreateAnyBackup,
                    PermissionPolicyIdentifiers.CreatePrivateBackup,
                    PermissionPolicyIdentifiers.ReadServiceLogs,
                    PermissionPolicyIdentifiers.ManageDispatcherQueues,
                    PermissionPolicyIdentifiers.ManageForeignData,
                    PermissionPolicyIdentifiers.ManageMail,
                    PermissionPolicyIdentifiers.AccessAuditLog,
                    PermissionPolicyIdentifiers.AlterSystemConfiguration,
                    PermissionPolicyIdentifiers.UnrestrictedAdministration
                }
            }
        };

        /// <inheritdoc/>
        public ClientPolicyDecisionProviderService(IPasswordHashingService hashService, IAdhocCacheService adhocCache = null) : base(hashService, adhocCache)
        {
            if(ApplicationServiceContext.Current.HostType == SanteDBHostType.Server)
            {
                throw new InvalidOperationException();
            }
        }

        /// <inheritdoc/>
        public override PolicyGrantType GetPolicyOutcome(IPrincipal principal, string policyId)
        {
            var mappedPolicy = this.m_policyMaps.FirstOrDefault(o => o.Value.Contains(policyId));
            if (!String.IsNullOrEmpty(mappedPolicy.Key))
            {
                policyId = mappedPolicy.Key;
            }
            return base.GetPolicyOutcome(principal, policyId);
        }

        /// <inheritdoc/>
        public override IEnumerable<IPolicyInstance> GetEffectivePolicySet(IPrincipal principal)
        {
            var cacheKey = this.ComputeCacheKey(principal);
            EffectivePolicyInstance[] basePolicySet = null;
            if (this.m_adhocCacheService?.TryGet(cacheKey, out basePolicySet) != true)
            {
                basePolicySet = base.GetEffectivePolicySet(principal).OfType<EffectivePolicyInstance>().ToArray();
                foreach (var pi in basePolicySet)
                {
                    if (this.m_policyMaps.TryGetValue(pi.Policy.Oid, out var mappedPolicies))
                    {
                        foreach (var mp in mappedPolicies)
                        {

                            basePolicySet.Where(o => o.Policy.Oid == mp || o.Policy.Oid.StartsWith($"{mp}.")).ForEach(basePolicy =>
                            {
                                if (pi.Rule >= basePolicy?.Rule) // Mapped local policy is more liberal than another policy so set the policy to the local policy
                                {
                                    basePolicy.Rule = pi.Rule;
                                }
                            });
                        }
                    }
                }
                this.m_adhocCacheService.Add(cacheKey, basePolicySet);
            }
            return basePolicySet.OfType<IPolicyInstance>();
        }

        /// <summary>
        /// Get all mapped policies in the input set
        /// </summary>
        public IEnumerable<string> ExpandInferredPolicies(IEnumerable<string> policyOids)
        {
            foreach(var itm in policyOids)
            {
                if(this.m_policyMaps.TryGetValue(itm, out var mappedPolicies))
                {
                    foreach(var p in mappedPolicies)
                    {
                        yield return p;
                    }
                }
            }
        }
    }
}
