using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SanteDB.Disconnected.Services
{
    /// <summary>
    /// Provides the SanteDB application gateway which binds the JavaScript
    /// layer with the host context
    /// </summary>
    public interface IAppletHostBridgeProvider
    {

        /// <summary>
        /// Get the bridge script which allows the client side JavaScript to 
        /// interact with the host
        /// </summary>
        String GetBridgeScript();
    }
}
