using SanteDB.Client.Configuration;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Configuration;
using SanteDB.Persistence.Auditing.ADO.Configuration;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.PubSub.ADO.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Disconnected.Configuration
{
    /// <summary>
    /// Represents a configuration feautre that can handle the "data"
    /// </summary>
    public class DataRestConfigurationFeature : IClientConfigurationFeature
    {

        public const string GLOBAL_DATA_PROVIDER_SETTING = "provider";
        public const string GLOBAL_CONNECTION_STRING_SETTING = "options";
        public const string CONNECTION_PER_FEATURE_SETTING = "connections";

        private readonly DataConfigurationSection m_connectStringSection;
        private readonly DataRetentionConfigurationSection m_retentionConfigurationSection;
        private readonly OrmConfigurationSection m_ormConfigurationSection;
        private readonly IEnumerable<OrmConfigurationBase> m_dataConfigurationSections;

        /// <summary>
        /// DI constructor
        /// </summary>
        public DataRestConfigurationFeature(IConfigurationManager configurationManager)
        {
            this.m_connectStringSection = configurationManager.GetSection<DataConfigurationSection>();
            this.m_retentionConfigurationSection = configurationManager.GetSection<DataRetentionConfigurationSection>();
            this.m_ormConfigurationSection = configurationManager.GetSection<OrmConfigurationSection>();
            this.m_dataConfigurationSections = configurationManager.Configuration.Sections.OfType<OrmConfigurationBase>().ToArray();
        }

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public string Name => "data";

        /// <inheritdoc/>
        public ConfigurationDictionary<string, object> Configuration => this.GetConfiguration();

        private ConfigurationDictionary<string, object> GetConfiguration()
        {
            var retVal = new ConfigurationDictionary<string, object>();

            retVal[CONNECTION_PER_FEATURE_SETTING] = this.m_dataConfigurationSections.ToDictionary(o => o.GetType().Name, o => this.ParseDataConfigurationSection(o));

            // Is there only one configuration section?
            if (this.m_connectStringSection.ConnectionString.Count == 1)
            {
                var ctOI = this.m_connectStringSection.ConnectionString.First();
                retVal[GLOBAL_DATA_PROVIDER_SETTING] = ctOI.Provider;
                retVal[GLOBAL_CONNECTION_STRING_SETTING] = this.ParseConnectionString(ctOI);
            }

            return retVal;
        }

        /// <summary>
        /// Parse data configuration section
        /// </summary>
        private IDictionary<String, Object> ParseDataConfigurationSection(OrmConfigurationBase ormSection)
        {
            var retVal = new ConfigurationDictionary<String, Object>();
            retVal[GLOBAL_DATA_PROVIDER_SETTING] = ormSection.Provider;
            var connectionString = this.m_connectStringSection.ConnectionString.Find(o => o.Name == ormSection.ReadWriteConnectionString);
            retVal[GLOBAL_CONNECTION_STRING_SETTING] = this.ParseConnectionString(connectionString);
            return retVal;
        }

        /// <summary>
        /// Parse a connection string
        /// </summary>
        private IDictionary<String, Object> ParseConnectionString(ConnectionString connectionString)
        {
            if (connectionString == null)
            {
                return new ConfigurationDictionary<String, Object>();
            }
            else
            {
                var provider = DataConfigurationSection.GetDataConfigurationProviders().First(o => o.Invariant == connectionString.Provider);
                return provider.ParseConnectionString(connectionString);
            }
        }

        /// <inheritdoc/>
        public string ReadPolicy => PermissionPolicyIdentifiers.AlterSystemConfiguration;

        /// <inheritdoc/>
        public string WritePolicy => PermissionPolicyIdentifiers.AlterSystemConfiguration;

        /// <inheritdoc/>
        public bool Configure(SanteDBConfiguration configuration, IDictionary<string, object> featureConfiguration)
        {
            throw new NotImplementedException();
        }
    }
}
