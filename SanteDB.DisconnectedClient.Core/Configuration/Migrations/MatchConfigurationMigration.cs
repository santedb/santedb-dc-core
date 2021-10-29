/*
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

using SanteDB.Core.Matching;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Services.Remote;
using SanteDB.Matcher.Configuration;
using SanteDB.Matcher.Definition;
using SanteDB.Matcher.Matchers;
using SanteDB.Matcher.Services;
using System.Collections.Generic;
using System.IO;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    /// <summary>
    /// Configuration migration for adding match services
    /// </summary>
    public class MatchConfigurationMigration : IConfigurationMigration
    {
        /// <summary>
        /// Description of the matching
        /// </summary>
        public string Description => "Configures the SanteDB configuration to include simple match services";

        /// <summary>
        /// Gets the id
        /// </summary>
        public string Id => "add-simple-matching-config-v4";

        /// <summary>
        /// Install the specified extension
        /// </summary>
        public bool Install()
        {
            // Connected to sync
            var syncMode = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>()?.Mode;
            if (!syncMode.HasValue || syncMode == SynchronizationMode.Online)
            {
                ApplicationContext.Current.RemoveServiceProvider(typeof(SimpleRecordMatchingService), true);
                ApplicationContext.Current.RemoveServiceProvider(typeof(FileMatchConfigurationProvider), true);
                ApplicationContext.Current.AddServiceProvider(typeof(RemoteRecordMatchConfigurationService), true);
            }
            else // user central server for checkout
            {
                ApplicationContext.Current.RemoveServiceProvider(typeof(RemoteRecordMatchConfigurationService), true);

                ApplicationContext.Current.AddServiceProvider(typeof(SimpleRecordMatchingService), true);
                ApplicationContext.Current.AddServiceProvider(typeof(FileMatchConfigurationProvider), true);

                // Setup the match configurations
                var fileConfig = ApplicationContext.Current.Configuration.GetSection<FileMatchConfigurationSection>();
                if (fileConfig == null)
                {
                    fileConfig = new FileMatchConfigurationSection
                    {
                        FilePath = new List<FilePathConfiguration>
                    {
                        new  FilePathConfiguration
                        {
                            Path = Path.Combine(ApplicationContext.Current.ConfigurationPersister.ApplicationDataDirectory, "matching"),
                            ReadOnly = false
                        }
                    }
                    };
                    ApplicationContext.Current.Configuration.AddSection(fileConfig);
                }
            }

            // Setup the approx configuration
            var approxConfig = ApplicationContext.Current.Configuration.GetSection<ApproximateMatchingConfigurationSection>();
            if (approxConfig == null)
            {
                approxConfig = new ApproximateMatchingConfigurationSection
                {
                    ApproxSearchOptions = new List<ApproxSearchOption>()
                };

                // Add pattern
                approxConfig.ApproxSearchOptions.Add(new ApproxPatternOption { Enabled = true, IgnoreCase = true });
                // Add soundex as preferred
                approxConfig.ApproxSearchOptions.Add(new ApproxPhoneticOption { Enabled = true, Algorithm = ApproxPhoneticOption.PhoneticAlgorithmType.Auto, MinSimilarity = 1.0f, MinSimilaritySpecified = true });
                // Add levenshtein
                approxConfig.ApproxSearchOptions.Add(new ApproxDifferenceOption { Enabled = true, MaxDifference = 1 });

                ApplicationContext.Current.Configuration.AddSection(approxConfig);
            }

            return true;
        }
    }
}