using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Data migration for security user key
    /// </summary>
    public class StripUserEntityUuidKey : IDataMigration
    {
        /// <summary>
        /// Get the description
        /// </summary>
        public string Description => "Strip required from the user entity table";

        /// <summary>
        /// Gets the id
        /// </summary>
        public string Id => "zzz-strip-dbuserentity-notnull";

        /// <summary>
        /// Install the extension
        /// </summary>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());

            try
            {

                // Database for the SQL Lite connection
                var db = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
                using (db.Lock())
                {
                    db.MigrateTable<DbUserEntity>();
                }
            }
            catch(Exception e)
            {
                tracer.TraceWarning("Could not apply migration to strip required from user entity uuid");
            }
            return true;
        }
    }
}
