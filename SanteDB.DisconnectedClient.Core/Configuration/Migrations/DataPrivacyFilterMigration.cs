using SanteDB.Core.Security.Privacy;
using SanteDB.DisconnectedClient.Configuration.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Configuration.Migrations
{
    public class DataPrivacyFilterMigration : IConfigurationMigration
    {
        /// <summary>
        /// Description of the matching
        /// </summary>
        public string Description => "Verifies policy is active";

        /// <summary>
        /// Gets the id
        /// </summary>
        public string Id => "add-policy-config";

        /// <summary>
        /// Install the specified extension
        /// </summary>
        public bool Install()
        {

            if(ApplicationContext.Current.GetService<DataPolicyFilterService>() == null) 
                ApplicationContext.Current.AddServiceProvider(typeof(DataPolicyFilterService), true);

            return true;
        }
    }
}
