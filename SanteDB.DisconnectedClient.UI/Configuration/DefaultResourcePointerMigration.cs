using SanteDB.Core;
using SanteDB.Core.Api.Services;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.UI.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    /// <summary>
    /// Configuration migration for default pointer
    /// </summary>
    public class DefaultResourcePointerMigration : IConfigurationMigration
    {
        /// <summary>
        /// ID of the migration
        /// </summary>
        public string Id => "999-ui-resource-pointer";

        /// <summary>
        /// Get the description
        /// </summary>
        public string Description => "Adds QR Code Generation and ResourcePointer Services";

        /// <summary>
        /// Install the services
        /// </summary>
        public bool Install()
        {
            if (ApplicationServiceContext.Current.GetService<IBarcodeProviderService>() == null)
                ApplicationContext.Current.AddServiceProvider(typeof(QrBarcodeGenerator), true);
            if (ApplicationServiceContext.Current.GetService<IResourcePointerService>() == null)
                ApplicationContext.Current.AddServiceProvider(typeof(JwsResourcePointerService), true);
            return true;
        }
    }
}
