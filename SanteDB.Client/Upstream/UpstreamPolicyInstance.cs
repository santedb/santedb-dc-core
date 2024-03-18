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
 * Date: 2023-6-21
 */
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;

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
            this.Policy = new GenericPolicy(policy.Key.GetValueOrDefault(), policy.Oid, policy.Name, policy.CanOverride);
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
