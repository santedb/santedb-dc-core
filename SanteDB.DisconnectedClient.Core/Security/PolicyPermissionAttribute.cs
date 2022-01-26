﻿/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */

using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using System;
using System.Security;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents a security attribute which requires that a user be in the possession of a
    /// particular claim
    /// </summary>
    public class PolicyPermissionAttribute : CodeAccessSecurityAttribute
    {
        /// <summary>
        /// Creates a policy permission attribute
        /// </summary>
        public PolicyPermissionAttribute(SecurityAction action) : base(action)
        {
        }

        /// <summary>
        /// The claim type which the user must
        /// </summary>
        public String PolicyId { get; set; }

        /// <summary>
        /// Permission
        /// </summary>
        public override IPermission CreatePermission()
        {
            return new PolicyPermission(PermissionState.Unrestricted, this.PolicyId);
        }
    }

    /// <summary>
    /// A policy permission
    /// </summary>
    [Serializable]
    public class PolicyPermission : IPermission, IUnrestrictedPermission
    {
        // True if unrestricted
        private bool m_isUnrestricted;

        private String m_policyId;
        private IPrincipal m_principal;

        // Security
        private Tracer m_traceSource = Tracer.GetTracer(typeof(PolicyPermission));

        /// <summary>
        /// Policy permission
        /// </summary>
        public PolicyPermission(PermissionState state, String policyId, IPrincipal principal)
        {
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));
            this.m_isUnrestricted = state == PermissionState.Unrestricted;
            this.m_policyId = policyId;
            this.m_principal = principal;
        }

        /// <summary>
        /// Creates a new policy permission
        /// </summary>
        public PolicyPermission(PermissionState state, String policyId) : base()
        {
            this.m_isUnrestricted = state == PermissionState.Unrestricted;
            this.m_policyId = policyId;
            this.m_principal = AuthenticationContext.Current.Principal;
        }

        /// <summary>
        /// Copy the permission
        /// </summary>
        public IPermission Copy()
        {
            return new PolicyPermission(this.m_isUnrestricted ? PermissionState.Unrestricted : PermissionState.None, this.m_policyId, this.m_principal);
        }

        /// <summary>
        /// Demand the permission
        /// </summary>
        public void Demand()
        {
            var pdp = ApplicationServiceContext.Current.GetService<IPolicyDecisionService>();
            var principal = this.m_principal ?? AuthenticationContext.Current.Principal;
            var action = AuthenticationContext.Current.Principal == AuthenticationContext.SystemPrincipal ? PolicyGrantType.Grant : PolicyGrantType.Deny;

            // Non system principals must be authenticated
            if (!principal.Identity.IsAuthenticated &&
                principal != AuthenticationContext.SystemPrincipal &&
                this.m_isUnrestricted == true)
                throw new PolicyViolationException(principal, this.m_policyId, PolicyGrantType.Deny);
            else
            {
                if (pdp == null) // No way to verify
                    action = PolicyGrantType.Deny;
                else if (pdp != null)
                    action = pdp.GetPolicyOutcome(principal, this.m_policyId);
            }

            this.m_traceSource.TraceVerbose("Policy Enforce: {0}({1}) = {2}", principal?.Identity?.Name, this.m_policyId, action);

            AuditUtil.AuditAccessControlDecision(principal, m_policyId, action);
            if (action != PolicyGrantType.Grant)
            {
                throw new PolicyViolationException(principal, this.m_policyId, action);
            }
        }

        /// <summary>
        /// From XML
        /// </summary>
        public void FromXml(SecurityElement elem)
        {
            string element = elem.Attribute("Unrestricted");
            if (element != null)
                this.m_isUnrestricted = Convert.ToBoolean(element);
            element = elem.Attribute("PolicyId");
            if (element != null)
                this.m_policyId = element;
            element = elem.Attribute("principal");
            if (element != null)
                this.m_principal = new GenericPrincipal(ApplicationContext.Current.IdentityProviderService.GetIdentity(element), null);
            else
                throw new InvalidOperationException("Must have policyid");
        }

        /// <summary>
        /// Intersect the permission
        /// </summary>
        public IPermission Intersect(IPermission target)
        {
            if (target == null)
                return null;
            if ((target as IUnrestrictedPermission)?.IsUnrestricted() == false)
                return target;
            else
                return this.Copy();
        }

        /// <summary>
        /// If the two operations allow the exact set of operations
        /// </summary>
        public bool IsSubsetOf(IPermission target)
        {
            if (target == null)
                return !this.m_isUnrestricted;
            else
            {
                var permission = target as PolicyPermission;
                return permission.m_isUnrestricted == this.m_isUnrestricted &&
                this.m_policyId.StartsWith(permission.m_policyId);
            }
        }

        /// <summary>
        /// True if the permission is unrestricted
        /// </summary>
        public bool IsUnrestricted()
        {
            return this.m_isUnrestricted;
        }

        /// <summary>
        /// Represent the element as XML
        /// </summary>
        public SecurityElement ToXml()
        {
            SecurityElement element = new SecurityElement("IPermission");
            Type type = this.GetType();
            StringBuilder AssemblyName = new StringBuilder(type.Assembly.ToString());
            AssemblyName.Replace('\"', '\'');
            element.AddAttribute("class", type.FullName + ", " + AssemblyName);
            element.AddAttribute("version", "1");
            element.AddAttribute("Unrestricted", this.m_isUnrestricted.ToString());
            element.AddAttribute("Policy", this.m_policyId);
            element.AddAttribute("Principal", this.m_principal.Identity.Name);
            return element;
        }

        public IPermission Union(IPermission target)
        {
            throw new NotImplementedException();
        }
    }
}