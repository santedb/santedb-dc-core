using SanteDB.Client.Configuration;
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Data;
using SanteDB.OrmLite.Configuration;
using SanteDB.OrmLite.Providers;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Disconnected.Configuration
{
    /// <summary>
    /// Database initial configuration section
    /// </summary>
    public class DataInitialConfigurationProvider : IInitialConfigurationProvider
    {
        /// <inheritdoc/>
        public SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration)
        {
            var ormSection = configuration.GetSection<OrmConfigurationSection>();
            if(ormSection == null)
            {
                ormSection = new OrmConfigurationSection();
                configuration.AddSection(ormSection);
            }

            ormSection.Providers = DataConfigurationSection.GetDataConfigurationProviders()
                .Where(o => o.HostType == hostContextType).Select(o => new ProviderRegistrationConfiguration(o.Invariant, o.DbProviderType)).ToList();
            ormSection.AdoProvider = AppDomain.CurrentDomain.GetAllTypes().Where(t => typeof(DbProviderFactory).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).Select(t => new ProviderRegistrationConfiguration(t.Namespace.StartsWith("System") ? t.Name : t.Namespace.Split('.')[0], t)).ToList();

            // Construct the connection strings and initial configurations for the orm configuration section base
            foreach(var itm in AppDomain.CurrentDomain.GetAllTypes().Where(t=>typeof(OrmConfigurationBase).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
            {
                if (configuration.GetSection(itm) == null)
                {
                    var sectionInstance = Activator.CreateInstance(itm) as OrmConfigurationBase;
                    configuration.AddSection(sectionInstance);
                }
            }

            // Construct the inital data section
            var dataSection = configuration.GetSection<DataConfigurationSection>();
            if(dataSection == null)
            {
                dataSection = new DataConfigurationSection();
                configuration.AddSection(dataSection);
            }
            dataSection.ConnectionString = new List<ConnectionString>();

            return configuration;
        }
    }
}
