using SanteDB.Core.PubSub;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Services.Remote;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    /// <summary>
    /// Adds the checkout manager
    /// </summary>
    public class PubSubManagerMigration : IConfigurationMigration
    {
        /// <summary>
        /// Gets the description of the migration
        /// </summary>
        public string Description => "Ensure appropriate checkout manager registered";

        /// <summary>
        /// Gets the identifier of the migration
        /// </summary>
        public string Id => "99c-pubsub-manager";

        /// <summary>
        /// Install the migration
        /// </summary>
        public bool Install()
        {
            if (ApplicationContext.Current.GetService<IPubSubManagerService>() == null)
            {
                // Connected to sync
                var syncMode = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>()?.Mode;
                if (!syncMode.HasValue || syncMode == SynchronizationMode.Online)
                {
                    ApplicationContext.Current.AddServiceProvider(typeof(RemotePubSubManager), true);
                }
                else // user central server for checkout
                {
                    //ApplicationContext.Current.AddServiceProvider(typeof(CachedResourceCheckoutService), true);
                }
            }
            return true;
        }
    }
}