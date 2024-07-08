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
 * User: trevor
 * Date: 2023-4-19
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Client.Shared
{
    /// <summary>
    /// The directory provider that will provide directories for storing data and configuration.
    /// </summary>
    public class LocalAppDirectoryProvider
    {
        readonly string m_AppDirectory;
        readonly string m_DataDirectory;
        readonly string m_ConfigDirectory;

        /// <summary>
        /// Instantiates a new instance of the provider.
        /// </summary>
        /// <param name="appName">The name of the app that will be used as part of the path for the configuration and data.</param>
        public LocalAppDirectoryProvider(string appName)
        {
            m_AppDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SanteDB", appName);
            m_ConfigDirectory = Path.Combine(m_AppDirectory, "config");
            m_DataDirectory = Path.Combine(m_AppDirectory, "data");

            AppDomain.CurrentDomain.SetData("DataDirectory", m_DataDirectory);
            AppDomain.CurrentDomain.SetData("ConfigDirectory", m_ConfigDirectory);
        }

        /// <summary>
        /// Will attempt to create any directories that do not exist. 
        /// </summary>
        /// <remarks>
        /// This does not catch any exceptions that could occur. This is to ensure a successful call indicates the directories will exist. 
        /// </remarks>
        private void EnsureDirectoriesAreCreated()
        {
            Directory.CreateDirectory(m_AppDirectory);
            Directory.CreateDirectory(m_ConfigDirectory);
            Directory.CreateDirectory(m_DataDirectory);
        }

        /// <summary>
        /// Retrieves the configuration file path and file name.
        /// </summary>
        /// <returns>The full path for the configuration file.</returns>
        public string GetConfigFilePath()
        {
            EnsureDirectoriesAreCreated();
            return Path.Combine(m_ConfigDirectory, "santedb.config");
        }

        
        /// <summary>
        /// Retrieves the configuration directory.
        /// </summary>
        /// <returns>The directory path for the configuration directory.</returns>
        public string GetConfigDirectory()
        {
            EnsureDirectoriesAreCreated();
            return m_ConfigDirectory;
        }

        /// <summary>
        /// Retrieves the data directory.
        /// </summary>
        /// <returns>The directory path for the data directory.</returns>
        public string GetDataDirectory()
        {
            EnsureDirectoriesAreCreated();
            return m_DataDirectory;
        }
    }
}
