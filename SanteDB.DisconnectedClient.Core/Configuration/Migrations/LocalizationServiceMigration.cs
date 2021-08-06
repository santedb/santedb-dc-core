using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    /// <summary>
    /// Adds localization service 
    /// </summary>
    public class LocalizationServiceMigration: IConfigurationMigration
    {
        /// <summary>
        /// Description of the matching
        /// </summary>
        public string Description => "Adds localization service";

        /// <summary>
        /// Gets the id
        /// </summary>
        public string Id => "add-locale-config";

        /// <summary>
        /// Install the specified extension
        /// </summary>
        public bool Install()
        {

            if (ApplicationContext.Current.GetService<ILocalizationService>() == null)
            {
                ApplicationContext.Current.AddServiceProvider(typeof(AppletLocalizationService), true);
            }
            return true;
        }
    }
}
