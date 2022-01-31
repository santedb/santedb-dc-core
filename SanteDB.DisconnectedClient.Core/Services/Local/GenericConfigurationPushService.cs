/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.DisconnectedClient.Services.Local
{
    /// <summary>
    /// A generic configuration push service 
    /// </summary>
    public class GenericConfigurationPushService : IConfigurationPushService
    {

        // Tracer for logs
        private Tracer m_tracer = Tracer.GetTracer(typeof(GenericConfigurationPushService));

        // Configuration targets
        private IDictionary<String, IConfigurationTarget> m_configurationTargets = null;

        /// <summary>
        /// Configure the specified target
        /// </summary>
        public List<Uri> Configure(Uri targetUri, string userName, string password, IDictionary<String, Object> configuration)
        {

            // Find the appropriate target
            try
            {
                var target = this.GetTarget(targetUri);
                if (target == null) throw new InvalidOperationException($"Cannot find configuration target implementation for {targetUri}");
                this.m_tracer.TraceVerbose("Will attempt to push configuration to {0} (package: {1})...", targetUri, target);
                return target.PushConfiguration(targetUri, userName, password, configuration);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error configuring {0} - {1}", targetUri, e);
                throw new Exception($"Error configuring target {targetUri}", e);
            }

        }

        /// <summary>
        /// Gets the specified target
        /// </summary>
        public IConfigurationTarget GetTarget(Uri targetUri)
        {
            if (targetUri == null) throw new ArgumentNullException("Invalid target URI");

            if (this.m_configurationTargets == null)
                this.m_configurationTargets = ApplicationServiceContext.Current.GetService<IServiceManager>().GetAllTypes()
                    .Where(o => typeof(IConfigurationTarget).IsAssignableFrom(o) && !o.IsAbstract && !o.IsInterface)
                    .Select(c => Activator.CreateInstance(c) as IConfigurationTarget)
                    .ToDictionary(o => o.Invariant, o => o);

            if (this.m_configurationTargets.TryGetValue(targetUri.Scheme, out IConfigurationTarget target))
                return target;
            else
                return null;
        }
    }
}
