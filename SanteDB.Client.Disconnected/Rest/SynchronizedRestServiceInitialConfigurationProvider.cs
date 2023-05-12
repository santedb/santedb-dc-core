﻿/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Rest.AppService;
using SanteDB.Rest.Common.Configuration;
using System;

namespace SanteDB.Client.Disconnected.Rest
{
    /// <summary>
    /// Configuration provider
    /// </summary>
    public class SynchronizedRestServiceInitialConfigurationProvider : IInitialConfigurationProvider
    {

        /// <inheritdoc/>
        public int Order => Int32.MaxValue;

        /// <summary>
        /// Provide the initial configuration
        /// </summary>
        public SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration)
        {
            if (hostContextType == SanteDBHostType.Test)
            {
                return configuration;
            }

            var restConfiguration = configuration.GetSection<RestConfigurationSection>().Services.Find(o => o.ConfigurationName == AppServiceMessageHandler.ConfigurationName);
            if (restConfiguration != null)
            {
                restConfiguration.ServiceType = typeof(SynchronizedAppServiceBehavior);
                restConfiguration.Endpoints.ForEach(o => o.Contract = typeof(ISynchronizedAppServiceContract));
            }
            return configuration;
        }
    }
}
