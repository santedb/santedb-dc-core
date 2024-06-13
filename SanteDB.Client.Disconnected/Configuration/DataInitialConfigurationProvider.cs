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
using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Matcher.Configuration;
using SanteDB.OrmLite.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.Client.Disconnected.Configuration
{
    /// <summary>
    /// Database initial configuration section
    /// </summary>
    public class DataInitialConfigurationProvider : IInitialConfigurationProvider
    {

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration)
        {
            var ormSection = configuration.GetSection<OrmConfigurationSection>();
            if (ormSection == null)
            {
                ormSection = new OrmConfigurationSection();
                configuration.AddSection(ormSection);
            }

            var providers = DataConfigurationSection.GetDataConfigurationProviders()
                .Where(o => o.HostType.HasFlag(hostContextType));
            ormSection.Providers = providers.Select(o => new ProviderRegistrationConfiguration(o.Invariant, o.DbProviderType)).ToList();
            ormSection.AdoProvider = providers.Select(t => new ProviderRegistrationConfiguration(t.Invariant, t.AdoNetFactoryType)).ToList();

            // Construct the connection strings and initial configurations for the orm configuration section base
            foreach (var itm in AppDomain.CurrentDomain.GetAllTypes().Where(t => typeof(OrmConfigurationBase).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
            {
                if (configuration.GetSection(itm) == null)
                {
                    var sectionInstance = Activator.CreateInstance(itm) as OrmConfigurationBase;
                    configuration.AddSection(sectionInstance);
                }
            }

            // Construct the inital data section
            var dataSection = configuration.GetSection<DataConfigurationSection>();
            if (dataSection == null)
            {
                dataSection = new DataConfigurationSection();
                dataSection.ConnectionString = new List<ConnectionString>();
                configuration.AddSection(dataSection);
            }

            var matchSection = configuration.GetSection<FileMatchConfigurationSection>();
            if (matchSection == null)
            {
                matchSection = new FileMatchConfigurationSection()
                {
                    CacheFiles = true,
                    FilePath = new List<Matcher.Definition.FilePathConfiguration>() {
                        new  Matcher.Definition.FilePathConfiguration() {
                             ReadOnly = false,
                            Path = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "matching")
                        }
                    }
                };
                configuration.AddSection(matchSection);
            }


            return configuration;
        }
    }
}
