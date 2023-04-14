using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Client.Shared
{
    public static class LocalAppDirectoryProviderExtensions
    {
        public static bool IsConfigFilePresent(this LocalAppDirectoryProvider provider)
            => File.Exists(provider.GetConfigFilePath());
    }
}
