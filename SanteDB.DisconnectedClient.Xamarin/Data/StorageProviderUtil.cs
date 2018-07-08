using SanteDB.DisconnectedClient.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static IEnumerable<IStorageProvider> GetProviders() => AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.ExportedTypes)
                        .Where(o => typeof(IStorageProvider).IsAssignableFrom(o))
                        .Select(t => Activator.CreateInstance(t) as IStorageProvider);

        /// <summary>
        /// Gets the specified storage provider
        /// </summary>
        /// <param name="invariantName">The name of the storage provider to retrieve</param>
        /// <returns>The registered storage provider</returns>
        public static IStorageProvider GetProvider(String invariantName) => GetProviders().First(o => o.Invariant == invariantName);
    }
}
