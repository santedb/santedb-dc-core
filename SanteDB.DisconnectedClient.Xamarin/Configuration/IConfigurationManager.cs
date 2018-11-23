﻿/*
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
 * User: fyfej
 * Date: 2017-9-1
 */
using SanteDB.DisconnectedClient.Core.Configuration;

namespace SanteDB.DisconnectedClient.Xamarin.Configuration
{
    /// <summary>
    /// Configuration manager for the application
    /// </summary>
    public interface IConfigurationManager
	{
		/// <summary>
		/// Returns true if SanteDB is configured
		/// </summary>
		bool IsConfigured { get; }

        /// <summary>
        /// Load the configuration
        /// </summary>
        void Load();

        /// <summary>
        /// Save the configuration to the default location
        /// </summary>
        void Save();

        /// <summary>
        /// Save the specified configuration
        /// </summary>
        /// <param name="config">Config.</param>
        void Save(SanteDBConfiguration config);
					
		/// <summary>
		/// Get the configuration
		/// </summary>
		SanteDBConfiguration Configuration {get; }

        /// <summary>
        /// Backs up the configuration
        /// </summary>
        void Backup();

        /// <summary>
        /// Has a backup?
        /// </summary>
        bool HasBackup();

        /// <summary>
        /// Restore a backup
        /// </summary>
        void Restore();

        /// <summary>
        /// Application data directory
        /// </summary>
        string ApplicationDataDirectory { get; }
	}
}

