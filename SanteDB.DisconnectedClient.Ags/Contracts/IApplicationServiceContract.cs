using RestSrvr.Attributes;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Contracts
{
    /// <summary>
    /// Application service contract
    /// </summary>
    [ServiceContract(Name = "APP")]
    public interface IApplicationServiceContract
    {

        /// <summary>
        /// Gets the routes
        /// </summary>
        [Get("/routes.js")]
        Stream GetRoutes();

        /// <summary>
        /// Get the configuration
        /// </summary>
        [Get("/Configuration")]
        ConfigurationViewModel GetConfiguration();

        /// <summary>
        /// Update configuration
        /// </summary>
        [Post("/Configuration")]
        ConfigurationViewModel UpdateConfiguration(ConfigurationViewModel configuration);

        /// <summary>
        /// Get the data storage providers
        /// </summary>
        [Get("/DataProviders")]
        List<StorageProviderViewModel> GetDataStorageProviders();

        /// <summary>
        /// Get user configuration
        /// </summary>
        [Get("/Configuration/User")]
        ConfigurationViewModel GetUserConfiguration(String userId);

        /// <summary>
        /// Get subscription definitions
        /// </summary>
        [Get("/SubscriptionDefinition")]
        List<AppletSubscriptionDefinition> GetSubscriptionDefinitions();

        /// <summary>
        /// Get locale assets
        /// </summary>
        [Get("/Locale")]
        Dictionary<String, String[]> GetLocaleAssets();

        /// <summary>
        /// Save the user configuration
        /// </summary>
        [Post("/Configuration/User")]
        ConfigurationViewModel SaveUserConfiguration(ConfigurationViewModel configuration);

        /// <summary>
        /// Join the realm
        /// </summary>
        [Post("/Configuration/Realm")]
        ConfigurationViewModel JoinRealm(ConfigurationViewModel configData);

    }
}
