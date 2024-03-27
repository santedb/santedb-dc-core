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
    public class ClientPolicyDecisionProviderService : DefaultPolicyDecisionService
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
                    PermissionPolicyIdentifiers.ManageDispatcherQueues,
                    PermissionPolicyIdentifiers.ManageForeignData,
                    PermissionPolicyIdentifiers.ManageMail,
                    PermissionPolicyIdentifiers.AccessAuditLog
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
    }
}
