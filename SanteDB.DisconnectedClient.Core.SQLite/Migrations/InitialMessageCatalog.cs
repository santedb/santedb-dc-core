using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Mail;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Represents an initial alert catalog
    /// </summary>
    public class InitialMessageCatalog : IDbMigration
    {

        /// <summary>
        /// Gets the id of this catalog
        /// </summary>
        public string Id => "000-init-mail";

        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description => "Mail message catalog";

        /// <summary>
        /// Installation
        /// </summary>
        public bool Install()
        {
            // Database for the SQL Lite connection
            var tracer = Tracer.GetTracer(typeof(InitialMessageCatalog));

            var db = SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current?.Configuration.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DataConfigurationSection>().MailDataStore).Value);
            try
            {
                using (db.Lock())
                {

                    db.BeginTransaction();

                    // Migration log create and check
                    db.CreateTable<DbMigrationLog>();
                    if (db.Table<DbMigrationLog>().Count(o => o.MigrationId == this.Id) > 0)
                    {
                        tracer.TraceWarning("Migration 000-init-alerts already installed");
                        return true;
                    }
                    else
                        tracer.TraceInfo("Installing initial alerts catalog");
                        db.Insert(new DbMigrationLog()
                        {
                            MigrationId = this.Id,
                            InstallationDate = DateTime.Now
                        });

                    db.CreateTable<DbMailMessage>();

                    db.Commit();

                    return true;
                }
            }
            catch(Exception e)
            {
                db.Rollback();
                tracer.TraceError("Error installing initial alerts catalog: {0}", e);
                throw;
            }

        }
    }
}
