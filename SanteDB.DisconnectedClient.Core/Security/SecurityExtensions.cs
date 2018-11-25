/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-11-23
 */
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.Security;

namespace SanteDB.DisconnectedClient.Core.Security
{
    public static class SecurityExtensions
    {
        /// <summary>
        /// Convert an IPolicy to a policy instance
        /// </summary>
        public static SecurityPolicyInstance ToPolicyInstance(this IPolicyInstance me)
        {
            return new SecurityPolicyInstance(
                new SecurityPolicy()
                {
                    CanOverride = me.Policy.CanOverride,
                    Oid = me.Policy.Oid,
                    Name = me.Policy.Name
                },
                (PolicyGrantType)(int)me.Rule
            );
        }

        /// <summary>
        /// Convert an IPolicy to a policy instance
        /// </summary>
        public static SecurityPolicyInstance ToPolicyInstance(this SecurityPolicyInfo me)
        {
            return new SecurityPolicyInstance(
                me.Policy,
                (PolicyGrantType)(int)me.Grant
            );
        }
    }
}
