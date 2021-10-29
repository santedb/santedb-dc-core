using SanteDB.Core.Matching;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Matcher.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Record matching configuration service
    /// </summary>
    public class RemoteRecordMatchConfigurationService : AmiRepositoryBaseService, IRecordMatchingConfigurationService
    {
        /// <summary>
        /// Get the configurations
        /// </summary>
        public IEnumerable<IRecordMatchingConfiguration> Configurations
        {
            get
            {
                try
                {
                    using (var client = this.GetClient())
                    {
                        return client.Client.Get<AmiCollection>("MatchConfiguration").CollectionItem.OfType<IRecordMatchingConfiguration>();
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Could not fetch configurations from upstream", e);
                }
            }
        }

        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "Remote AMI Match Configuration";

        /// <summary>
        /// Delete configuration
        /// </summary>
        public IRecordMatchingConfiguration DeleteConfiguration(string configurationId)
        {
            try
            {
                using (var client = this.GetClient())
                {
                    return client.Client.Delete<MatchConfiguration>($"MatchConfiguration/{configurationId}");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Could not remove configuration {configurationId} from upstream", e);
            }
        }

        /// <summary>
        /// Get a single configuration
        /// </summary>
        public IRecordMatchingConfiguration GetConfiguration(string configurationId)
        {
            try
            {
                using (var client = this.GetClient())
                {
                    return client.Client.Get<MatchConfiguration>($"MatchConfiguration/{configurationId}");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Could not retrieve configuration {configurationId} from upstream", e);
            }
        }

        /// <summary>
        /// Save configuration to upstream
        /// </summary>
        public IRecordMatchingConfiguration SaveConfiguration(IRecordMatchingConfiguration configuration)
        {
            try
            {
                using (var client = this.GetClient())
                {
                    if (configuration is MatchConfiguration sc)
                        return client.Client.Post<MatchConfiguration, MatchConfiguration>($"MatchConfiguration", sc);
                    else
                        throw new InvalidOperationException("Can't understand this match configuration type");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Could not save configuration {configuration} to upstream", e);
            }
        }
    }
}