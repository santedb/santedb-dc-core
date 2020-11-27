/*
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2020-5-2
 */
using SanteDB.Core;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Data
{
    /// <summary>
    /// Storage provider utility class
    /// </summary>
    public static class StorageProviderUtil
    {

        // Existing data providers
        private static IEnumerable<IDataConfigurationProvider> m_existing;

        /// <summary>
        /// Gets providers for the specified platform
        /// </summary>
        public static IEnumerable<IDataConfigurationProvider> GetProviders()
        {
            if(m_existing == null)
                m_existing = ApplicationServiceContext.Current.GetService<IServiceManager>().GetAllTypes()
                        .Where(o => typeof(IDataConfigurationProvider).IsAssignableFrom(o) && !o.GetTypeInfo().IsInterface && !o.GetTypeInfo().IsAbstract)
                        .Select(t => Activator.CreateInstance(t) as IDataConfigurationProvider).ToArray();
            return m_existing;
        }
                        

        /// <summary>
        /// Gets the specified storage provider
        /// </summary>
        /// <param name="invariantName">The name of the storage provider to retrieve</param>
        /// <returns>The registered storage provider</returns>
        public static IDataConfigurationProvider GetProvider(String invariantName) => GetProviders().First(o => o.Invariant == invariantName);

    }
}
