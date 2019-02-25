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
 * User: justin
 * Date: 2018-7-23
 */
using SanteDB.Core.Configuration.Data;
using SanteDB.DisconnectedClient.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Xamarin.Data
{
    /// <summary>
    /// Storage provider utility class
    /// </summary>
    public static class StorageProviderUtil
    {

        /// <summary>
        /// Gets providers for the specified platform
        /// </summary>
        public static IEnumerable<IDataProvider> GetProviders() => AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.IsDynamic)
                        .SelectMany(a => a.ExportedTypes)
                        .Where(o => typeof(IDataProvider).IsAssignableFrom(o) && !o.GetTypeInfo().IsInterface && !o.GetTypeInfo().IsAbstract)
                        .Select(t => Activator.CreateInstance(t) as IDataProvider);

        /// <summary>
        /// Gets the specified storage provider
        /// </summary>
        /// <param name="invariantName">The name of the storage provider to retrieve</param>
        /// <returns>The registered storage provider</returns>
        public static IDataProvider GetProvider(String invariantName) => GetProviders().First(o => o.Invariant == invariantName);

    }
}
