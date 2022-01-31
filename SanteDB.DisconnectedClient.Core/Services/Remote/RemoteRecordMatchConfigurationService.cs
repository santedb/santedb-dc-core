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
 * Date: 2021-10-28
 */
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