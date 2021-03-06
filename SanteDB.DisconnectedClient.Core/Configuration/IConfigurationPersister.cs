﻿/*
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
using SanteDB.Core.Configuration;

namespace SanteDB.DisconnectedClient.Configuration
{
    /// <summary>
    /// Configuration manager for the application
    /// </summary>
    public interface IConfigurationPersister
    {
	    /// <summary>
        /// Application data directory
        /// </summary>
        string ApplicationDataDirectory { get; }

	    /// <summary>
        /// Returns true if SanteDB is configured
        /// </summary>
        bool IsConfigured { get; }

	    /// <summary>
        /// Backs up the configuration
        /// </summary>
        void Backup(SanteDBConfiguration configuration);

	    /// <summary>
        /// Has a backup?
        /// </summary>
        bool HasBackup();

	    /// <summary>
        /// Load the configuration
        /// </summary>
        SanteDBConfiguration Load();

	    /// <summary>
        /// Restore a backup
        /// </summary>
        SanteDBConfiguration Restore();

	    /// <summary>
        /// Save the configuration to the default location
        /// </summary>
        void Save(SanteDBConfiguration configuration);
    }
}

