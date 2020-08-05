using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.Matcher.Configuration;
using SanteDB.Matcher.Matchers;
using SanteDB.Matcher.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    /// <summary>
    /// Configuration migration for adding match services
    /// </summary>
    public class MatchConfigurationMigration : IConfigurationMigration
    {
        /// <summary>
        /// Gets the id
        /// </summary>
        public string Id => "add-simple-matching-config";

        /// <summary>
        /// Description of the matching
        /// </summary>
        public string Description => "Configures the SanteDB configuration to include simple match services";

        /// <summary>
        /// Install the specified extension
        /// </summary>
        public bool Install()
        {

            ApplicationContext.Current.AddServiceProvider(typeof(SimpleRecordMatchingService), true);
            ApplicationContext.Current.AddServiceProvider(typeof(FileMatchConfigurationProvider), true);

            // Setup the match configurations
            var fileConfig = ApplicationContext.Current.Configuration.GetSection<FileMatchConfigurationSection>();
            if (fileConfig == null) { 
                fileConfig = new FileMatchConfigurationSection()
                {
                    FilePath = new List<FilePathConfiguration>() {
                        new  FilePathConfiguration() {
                            Path = Path.Combine(ApplicationContext.Current.ConfigurationPersister.ApplicationDataDirectory, "matching"),
                            ReadOnly = false
                        }
                    }
                };
                ApplicationContext.Current.Configuration.AddSection(fileConfig);
            }

            // Setup the approx configuration
            var approxConfig = ApplicationContext.Current.Configuration.GetSection<ApproximateMatchingConfigurationSection>();
            if(approxConfig == null)
            {
                approxConfig = new ApproximateMatchingConfigurationSection()
                {
                    ApproxSearchOptions = new List<ApproxSearchOption>()
                };

                // Add pattern
                approxConfig.ApproxSearchOptions.Add(new ApproxPatternOption() { Enabled = true, IgnoreCase = true });
                // Add soundex as preferred
                approxConfig.ApproxSearchOptions.Add(new ApproxPhoneticOption() { Enabled = true, Algorithm = ApproxPhoneticOption.PhoneticAlgorithmType.Auto, MinSimilarity = 1.0f, MinSimilaritySpecified = true });
                // Add levenshtein
                approxConfig.ApproxSearchOptions.Add(new ApproxDifferenceOption() { Enabled = true, MaxDifference = 1 });

                ApplicationContext.Current.Configuration.AddSection(approxConfig);
            }

            return true;
        }
    }
}
