﻿/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Client.Upstream.Management;
using SanteDB.Client.UserInterface.Impl;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Diagnostics.Tracing;
using SanteDB.Core.Services;
using System;
using System.Linq;

namespace SanteDB.Client
{
    /// <summary>
    /// A service factory that creates the default client services
    /// </summary>
    public class DefaultClientServiceFactory : IServiceFactory
    {
        private readonly Type[] m_serviceTypes = new Type[]
        {
            typeof(DefaultUserPreferenceManager),
            typeof(DefaultUpstreamManagementService),
            typeof(DefaultUpstreamAvailabilityProvider),
            typeof(DefaultUpstreamIntegrationService),
            typeof(UpstreamDiagnosticRepository),
            typeof(RolloverLogManagerService),
            typeof(AppletNotificationTemplateRepository)
        };
        private readonly IServiceManager m_serviceManager;

        /// <summary>
        /// Client service factory
        /// </summary>
        public string ServiceName => "Default Client Service Factory";

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultClientServiceFactory(IServiceManager serviceManager)
        {
            this.m_serviceManager = serviceManager;
        }

        /// <inheritdoc/>
        public bool TryCreateService<TService>(out TService serviceInstance)
        {
            if (this.TryCreateService(typeof(TService), out var nonGenService))
            {
                serviceInstance = (TService)nonGenService;
                return true;
            }
            serviceInstance = default(TService);
            return false;
        }

        /// <inheritdoc/>
        public bool TryCreateService(Type serviceType, out object serviceInstance)
        {
            var fixedSerivce = this.m_serviceTypes.FirstOrDefault(t => serviceType.IsAssignableFrom(t));
            if (fixedSerivce != null)
            {
                serviceInstance = this.m_serviceManager.CreateInjected(fixedSerivce);
                return true;
            }
            serviceInstance = null;
            return false;
        }
    }
}
