/*
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
using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Xml.Serialization;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Xamarin.Http;
using SanteDB.DisconnectedClient.Xamarin.Diagnostics;
using System.Collections.Generic;
using SanteDB.Core.Diagnostics;
using System.Security.Cryptography;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Services.Impl;
using SanteDB.DisconnectedClient.Xamarin.Security;
using SanteDB.DisconnectedClient.Core.Data;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using SanteDB.DisconnectedClient.Xamarin.Threading;
using SanteDB.DisconnectedClient.Core.Caching;
using SanteDB.DisconnectedClient.Core.Alerting;
using SanteDB.Core.Services.Impl;
using SanteDB.Core.Protocol;

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

