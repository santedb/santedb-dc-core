using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Upstream
{
    /// <summary>
    /// Represents an <see cref="IPolicyInstance"/> which was derived from AMI information
    /// </summary>
    internal class UpstreamPolicyInstance : IPolicyInstance
    {

        /// <summary>
        /// Creates a new policy instance
        /// </summary>
        public UpstreamPolicyInstance(object securable, SecurityPolicy policy, PolicyGrantType grant)
        {
            this.Policy = new GenericPolicy(policy.Key.Value, policy.Oid, policy.Name, policy.CanOverride);
            this.Securable = securable;
            this.Rule = grant;
        }

        /// <inheritdoc/>
        public IPolicy Policy { get; }

        /// <inheritdoc/>
        public PolicyGrantType Rule { get; }

        /// <inheritdoc/>
        public object Securable { get; }
    }
}
