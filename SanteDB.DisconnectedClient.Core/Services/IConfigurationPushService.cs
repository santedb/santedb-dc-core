using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Services
{

    /// <summary>
    /// An interface which can push and/or configure the DCG configuration with a third party
    /// </summary>
    public interface IConfigurationPushService
    {

        /// <summary>
        /// Configure the specified target device with the specified username and software
        /// </summary>
        List<Uri> Configure(Uri targetUri, String userName, String password, IDictionary<String, Object> configuration);

        /// <summary>
        /// Gets the specified remote software package 
        /// </summary>
        IConfigurationTarget GetTarget(Uri targetUri);

        // TODO: Add more methods to this which will be more useful for future configuration solutions
    }
}
