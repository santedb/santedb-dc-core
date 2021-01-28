using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.UI.Services
{
    /// <summary>
    /// A SHIM generator implementation (overrides the default SHIM generation process)
    /// </summary>
    public interface IShimGenerator
    {
        /// <summary>
        /// Gets the SHIM methods for this runtime environment
        /// </summary>
        String GetShimMethods();

    }
}
