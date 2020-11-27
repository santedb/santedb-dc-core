﻿/*
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents the policy decision service
    /// </summary>
    public class DefaultPolicyDecisionService : IPolicyDecisionService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Default Policy Decision Service";

        // Policy cache
        private ConcurrentDictionary<String, ConcurrentDictionary<String, dynamic>> m_policyCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, dynamic>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Security.PolicyDecisionService"/> class.
        /// </summary>
        public DefaultPolicyDecisionService()
        {
        }

        /// <summary>
        /// Get a policy decision for a particular securable
        /// </summary>
        public PolicyDecision GetPolicyDecision(IPrincipal principal, object securable)
        {
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));
            else if (securable == null)
                throw new ArgumentNullException(nameof(securable));

            // Get the user object from the principal
            var pip = ApplicationContext.Current.PolicyInformationService;

            // Policies
            var securablePolicies = pip.GetActivePolicies(securable);

            // Most restrictive
            List<PolicyDecisionDetail> dtls = new List<PolicyDecisionDetail>();
            var retVal = new PolicyDecision(securable, dtls);
            foreach (var pol in securablePolicies)
            {
                var securablePdp = this.GetPolicyOutcome(principal, pol.Policy.Oid);
                dtls.Add(new PolicyDecisionDetail(pol.Policy.Oid, securablePdp));
            }

            return retVal;
        }

        /// <summary>
        /// Get a policy decision outcome (i.e. make a policy decision)
        /// </summary>
        public PolicyGrantType GetPolicyOutcome(IPrincipal principal, string policyId)
        {
            ConcurrentDictionary<String, dynamic> grants = null;
            PolicyGrantType rule;

            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }
            else if (String.IsNullOrEmpty(policyId))
            {
                throw new ArgumentNullException(nameof(policyId));
            }
            else if (this.m_policyCache.TryGetValue(principal.Identity.Name, out grants) &&
                grants.TryGetValue(policyId, out dynamic data))
            {
                if (DateTime.Now.Subtract(data.Time).TotalSeconds > 120)
                    grants.TryRemove(policyId, out dynamic v);
                else
                    return data.Rule;
            }

            // Can we make this decision based on the claims?
            if (principal is IClaimsPrincipal && (principal as IClaimsPrincipal).HasClaim(c => c.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim && (c.Value == policyId || policyId.StartsWith(c.Value))))
            {
                rule = PolicyGrantType.Grant;
            }
            else
            {
                // Get the user object from the principal
                var pip = ApplicationContext.Current.PolicyInformationService;

                if (pip == null)
                    return PolicyGrantType.Deny;

                // Policies
                var activePolicies = pip.GetActivePolicies(principal).Where(o => policyId == o.Policy.Oid || policyId.StartsWith(String.Format("{0}.", o.Policy.Oid)));

                // Most restrictive
                IPolicyInstance policyInstance = null;

                foreach (var pol in activePolicies)
                {
                    if (policyInstance == null)
                    {
                        policyInstance = pol;
                    }
                    else if (pol.Rule < policyInstance.Rule)
                    {
                        policyInstance = pol;
                    }
                }

                if (policyInstance == null)
                {
                    // TODO: Configure OptIn or OptOut
                    rule = PolicyGrantType.Deny;
                }
                else if (!policyInstance.Policy.CanOverride && policyInstance.Rule == PolicyGrantType.Elevate)
                {
                    rule = PolicyGrantType.Deny;
                }
                else if (!policyInstance.Policy.IsActive)
                {
                    rule = PolicyGrantType.Grant;
                }
                else
                    rule = policyInstance.Rule;

            } // db lookup

            // Add to local policy cache
                if (!this.m_policyCache.ContainsKey(principal.Identity.Name))
                {
                    grants = new ConcurrentDictionary<string, dynamic>();
                    this.m_policyCache.TryAdd(principal.Identity.Name, grants);
                }
                else if (grants == null)
                    grants = this.m_policyCache[principal.Identity.Name];
                if (!grants.ContainsKey(policyId))
                    try
                    {
                        grants.TryAdd(policyId, new { Rule = rule, Time = DateTime.Now });
                    }
                    catch
                    {
                        this.m_policyCache.TryRemove(principal.Identity.Name, out grants);
                        return rule;
                    }
            return rule;
        }
    }
}