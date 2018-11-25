using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Local extension types
    /// </summary>
    public class LocalExtensionTypeRepository : GenericLocalMetadataRepository<ExtensionType>, IExtensionTypeRepository
    {
        /// <summary>
        /// Get the xtension type by uri
        /// </summary>
        public ExtensionType Get(Uri uri)
        {
            var name = uri.ToString();
            int t;
            return base.Find(o => o.Name == name, 0, 1, out t).FirstOrDefault();
        }
    }
}
